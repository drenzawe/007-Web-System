using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Mail;
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
    UserManager<ApplicationUser> users,
    SmtpEmailSender emailSender,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? month,
        [FromQuery] string? year,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(month) && !string.IsNullOrWhiteSpace(year))
        {
            return BadRequest(new { message = "Choose either a month or a year for statistics." });
        }

        string start;
        string end;
        string period;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(
                    month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth) ||
                parsedMonth.Year > 9998)
            {
                return BadRequest(new { message = "Month must use the yyyy-MM format." });
            }

            start = parsedMonth.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
            end = parsedMonth.AddMonths(1).ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
            period = month;
        }
        else if (!string.IsNullOrWhiteSpace(year))
        {
            if (!int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear) ||
                parsedYear is < 1 or > 9998)
            {
                return BadRequest(new { message = "Year must be a valid four-digit year." });
            }

            start = $"{parsedYear:D4}-01-01";
            end = $"{parsedYear + 1:D4}-01-01";
            period = year;
        }
        else
        {
            return BadRequest(new { message = "Provide a month or year for statistics." });
        }

        var reservations = database.Reservations
            .AsNoTracking()
            .Where(reservation =>
                string.Compare(reservation.ReservationDate, start) >= 0 &&
                string.Compare(reservation.ReservationDate, end) < 0);

        var counts = await reservations
            .GroupBy(reservation => reservation.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var uniqueCustomers = await reservations
            .Select(reservation => reservation.CustomerId)
            .Where(customerId => customerId != string.Empty)
            .Distinct()
            .CountAsync(cancellationToken);

        var pending = counts.GetValueOrDefault("Pending");
        var approved = counts.GetValueOrDefault("Approved");
        var rejected = counts.GetValueOrDefault("Rejected");

        return Ok(new
        {
            period,
            totalReservations = pending + approved + rejected,
            pendingReservations = pending,
            approvedReservations = approved,
            rejectedReservations = rejected,
            uniqueCustomers
        });
    }

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
        await NotifyCustomerOfStatusAsync(reservation);

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
        await NotifyCustomerOfStatusAsync(reservation);

        return Ok(new { message = "Reservation rejected and its time slot released." });
    }

    private async Task NotifyCustomerOfStatusAsync(Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(reservation.Email))
        {
            logger.LogWarning(
                "Could not email customer about reservation {ReservationId}: no customer email is recorded.",
                reservation.Id);
            return;
        }

        if (!emailSender.IsConfigured)
        {
            logger.LogWarning(
                "Could not email customer about reservation {ReservationId}: SMTP is not configured.",
                reservation.Id);
            return;
        }

        try
        {
            await emailSender.SendReservationStatusAsync(
                reservation.Email,
                reservation.CustomerName,
                reservation.ReservationDate,
                reservation.ReservationTime,
                reservation.ServiceType,
                reservation.Status,
                reservation.RejectionReason,
                reservation.TravelFeeCents);
        }
        catch (SmtpException exception)
        {
            logger.LogError(
                exception,
                "Failed to email customer about reservation {ReservationId}.",
                reservation.Id);
        }
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
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
