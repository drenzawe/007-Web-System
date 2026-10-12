using System.ComponentModel.DataAnnotations;
using System.Globalization;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController(
    AppDbContext database,
    UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations(
        [FromQuery] string? status,
        [FromQuery] string? month,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) &&
            status is not ("Pending" or "Approved" or "Rejected"))
        {
            return BadRequest(new { message = "Status must be Pending, Approved, or Rejected." });
        }

        var reservations = database.Reservations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            reservations = reservations.Where(reservation => reservation.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(
                    month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
            {
                return BadRequest(new { message = "Month must use the yyyy-MM format." });
            }

            var start = parsedMonth.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
            var end = parsedMonth.AddMonths(1).ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
            reservations = reservations.Where(reservation =>
                string.Compare(reservation.ReservationDate, start) >= 0 &&
                string.Compare(reservation.ReservationDate, end) < 0);
        }

        var results = await reservations
            .OrderBy(reservation => reservation.ReservationDate)
            .ThenBy(reservation => reservation.ReservationTime)
            .Select(reservation => new
            {
                reservation.Id,
                date = reservation.ReservationDate,
                time = reservation.ReservationTime,
                name = reservation.CustomerName,
                email = reservation.Email,
                phone = reservation.CustomerPhone,
                city = reservation.City,
                address = reservation.LocationAddress,
                serviceType = reservation.ServiceType,
                travelFeeCents = reservation.TravelFeeCents,
                status = reservation.Status,
                rejectionReason = reservation.RejectionReason,
                createdAtUtc = reservation.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpPost("reservations/{id:int}/approve")]
    public async Task<IActionResult> ApproveReservation(
        int id,
        [FromBody] ApproveReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reservation is null)
        {
            return NotFound(new { message = "Reservation not found." });
        }
        if (reservation.Status != "Pending")
        {
            return Conflict(new { message = "Only pending reservations can be approved." });
        }

        if (reservation.ServiceType == "Workshop" && request.TravelFeeCents != 0)
        {
            return BadRequest(new { message = "Workshop appointments cannot have a home-service travel fee." });
        }

        reservation.TravelFeeCents = request.TravelFeeCents;
        reservation.Status = "Approved";
        await database.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Reservation approved." });
    }

    [HttpPost("reservations/{id:int}/reject")]
    public async Task<IActionResult> RejectReservation(
        int id,
        [FromBody] RejectReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reservation is null)
        {
            return NotFound(new { message = "Reservation not found." });
        }
        if (reservation.Status != "Pending")
        {
            return Conflict(new { message = "Only pending reservations can be rejected." });
        }

        reservation.Status = "Rejected";
        reservation.RejectionReason = request.Reason.Trim();
        await database.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Reservation rejected and its time slot released." });
    }

    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins()
    {
        var admins = await users.GetUsersInRoleAsync(AppRoles.Admin);
        return Ok(admins.Select(user => new
        {
            user.Id,
            name = user.DisplayName,
            email = user.Email
        }));
    }

    [HttpPost("admins")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.Name.Trim()
        };
        var created = await users.CreateAsync(admin, request.Password);
        if (!created.Succeeded)
        {
            return BadRequest(new
            {
                message = "The admin account could not be created.",
                errors = created.Errors.Select(error => error.Description)
            });
        }

        var assigned = await users.AddToRoleAsync(admin, AppRoles.Admin);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(admin);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The admin account could not be assigned an admin role.");
        }

        return Ok(new
        {
            admin.Id,
            name = admin.DisplayName,
            email = admin.Email
        });
    }
}

public class ApproveReservationRequest
{
    [Range(0, 100000000)]
    public int TravelFeeCents { get; set; }
}

public class RejectReservationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public class CreateAdminRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
}
