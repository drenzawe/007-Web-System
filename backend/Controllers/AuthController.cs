using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Web;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager,
    SmtpEmailSender emailSender,
    IConfiguration configuration,
    IAuthenticationSchemeProvider schemes,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProviders() =>
        Ok(new
        {
            google = await schemes.GetSchemeAsync("Google") is not null,
            facebook = await schemes.GetSchemeAsync("Facebook") is not null
        });

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!emailSender.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Email verification is not configured yet. Contact the site administrator."
            });
        }

        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.Name.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Conflict(new { message = "An account with this email already exists." });
            }

            return BadRequest(new
            {
                message = "The account could not be created.",
                errors = created.Errors.Select(error => error.Description)
            });
        }

        var assigned = await users.AddToRoleAsync(user, AppRoles.Customer);
        if (!assigned.Succeeded)
        {
            await DeleteUnconfirmedUserAsync(user);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The account could not be assigned a customer role.");
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationUrl = $"{GetFrontendBaseUrl()}/confirm-email?userId={HttpUtility.UrlEncode(user.Id)}&token={HttpUtility.UrlEncode(encodedToken)}";

        try
        {
            await emailSender.SendConfirmationAsync(email, confirmationUrl);
        }
        catch (System.Net.Mail.SmtpException exception)
        {
            logger.LogError(exception, "Failed to send an account confirmation email.");
            await DeleteUnconfirmedUserAsync(user);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The verification email could not be sent. Please try again later."
            });
        }

        return Accepted(new { message = "Check your email for a confirmation link to activate your account." });
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return BadRequest(new { message = "This confirmation link is invalid or has expired." });
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "This confirmation link is invalid or has expired." });
        }

        var confirmed = await users.ConfirmEmailAsync(user, token);
        if (!confirmed.Succeeded)
        {
            return BadRequest(new { message = "This confirmation link is invalid or has expired." });
        }

        return Ok(new { message = "Your email is confirmed. You can now sign in." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim();
        var result = await signInManager.PasswordSignInAsync(
            email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Ok(await CreateAccountResponseAsync(await users.FindByEmailAsync(email)));
        }
        if (result.IsNotAllowed)
        {
            return Unauthorized(new { message = "Confirm your email address before signing in." });
        }
        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked,
                new { message = "Too many failed sign-in attempts. Try again in 15 minutes." });
        }

        return Unauthorized(new { message = "The email or password is incorrect." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null
            ? Unauthorized()
            : Ok(await CreateAccountResponseAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var scheme = await schemes.GetSchemeAsync(provider);
        if (scheme is null || provider is not ("Google" or "Facebook"))
        {
            return NotFound(new { message = "This sign-in provider is not configured." });
        }

        var callbackUrl = Url.ActionLink(
            nameof(ExternalCallback),
            values: new { returnUrl = NormalizeReturnUrl(returnUrl) });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl!);
        return Challenge(properties, provider);
    }

    [HttpGet("external-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback([FromQuery] string? returnUrl, [FromQuery] string? remoteError)
    {
        var target = $"{GetFrontendBaseUrl()}{NormalizeReturnUrl(returnUrl)}";
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            return Redirect(AddQuery(target, "authError", "Social sign-in was cancelled or could not be completed."));
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return Redirect(AddQuery(target, "authError", "The social sign-in response could not be validated."));
        }

        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);
        if (result.Succeeded)
        {
            return Redirect(AddQuery(target, "auth", "success"));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            await signInManager.SignOutAsync();
            return Redirect(AddQuery(target, "authError", "The provider did not share a verified email address."));
        }

        if (info.LoginProvider == "Google" &&
            info.Principal.FindFirst("email_verified")?.Value != "true")
        {
            return Redirect(AddQuery(target, "authError", "Google did not verify the email address."));
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName
            };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                return Redirect(AddQuery(target, "authError", "Your account could not be created."));
            }

            var assigned = await users.AddToRoleAsync(user, AppRoles.Customer);
            if (!assigned.Succeeded)
            {
                await DeleteUnconfirmedUserAsync(user);
                return Redirect(AddQuery(target, "authError", "Your account could not be assigned a customer role."));
            }
        }
        else if (!user.EmailConfirmed)
        {
            return Redirect(AddQuery(target, "authError", "Confirm your email before linking this sign-in provider."));
        }

        var linked = await users.AddLoginAsync(user, info);
        if (!linked.Succeeded)
        {
            logger.LogWarning("An external sign-in account could not be linked.");
            return Redirect(AddQuery(target, "authError", "This social account could not be linked."));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Redirect(AddQuery(target, "auth", "success"));
    }

    [HttpGet("my-reservations")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> MyReservations(
        [FromServices] AppDbContext database,
        CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        var reservations = await database.Reservations
            .Where(reservation => reservation.CustomerId == userId)
            .OrderByDescending(reservation => reservation.ReservationDate)
            .ThenByDescending(reservation => reservation.ReservationTime)
            .Select(reservation => new
            {
                reservation.Id,
                date = reservation.ReservationDate,
                time = reservation.ReservationTime,
                serviceType = reservation.ServiceType,
                status = reservation.Status,
                rejectionReason = reservation.RejectionReason,
                travelFeeCents = reservation.TravelFeeCents
            })
            .ToListAsync(cancellationToken);

        return Ok(reservations);
    }

    private async Task<object> CreateAccountResponseAsync(ApplicationUser? user)
    {
        if (user is null)
        {
            return new { name = string.Empty, email = string.Empty, isAdmin = false };
        }

        return new
        {
            name = user.DisplayName,
            email = user.Email ?? string.Empty,
            isAdmin = await users.IsInRoleAsync(user, AppRoles.Admin)
        };
    }

    private async Task DeleteUnconfirmedUserAsync(ApplicationUser user)
    {
        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            logger.LogError("Could not remove an incomplete account after account setup failed.");
        }
    }

    private string GetFrontendBaseUrl() =>
        (configuration["Frontend:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');

    private static string NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith('/') &&
        !returnUrl.StartsWith("//") &&
        !returnUrl.Contains('\\')
            ? returnUrl
            : "/";

    private static string AddQuery(string url, string key, string value) =>
        QueryHelpers.AddQueryString(url, key, value);
}

public class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class ConfirmEmailRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
