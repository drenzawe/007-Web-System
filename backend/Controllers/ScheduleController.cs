using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Mail;
using backend;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/schedule")]
public class ScheduleController : ControllerBase
{
    private const int DailyCapacity = 3;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SmtpEmailSender _emailSender;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(
        AppDbContext context,
        UserManager<ApplicationUser> users,
        SmtpEmailSender emailSender,
        ILogger<ScheduleController> logger)
    {
        _context = context;
        _users = users;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetMonth([FromQuery] string? month)
    {
        if (month is null || !DateOnly.TryParseExact(
                month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
        {
            return BadRequest(new { message = "Month must use the yyyy-MM format." });
        }

        var firstDay = new DateOnly(parsedMonth.Year, parsedMonth.Month, 1);
        var start = firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = firstDay.AddMonths(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var unavailableDates = await _context.UnavailableDates
            .Where(date => string.Compare(date.Date, start) >= 0 && string.Compare(date.Date, end) < 0)
            .Select(date => date.Date)
            .ToListAsync();
        var reservations = await _context.Reservations
            .Where(reservation =>
                string.Compare(reservation.ReservationDate, start) >= 0 &&
                string.Compare(reservation.ReservationDate, end) < 0 &&
                (reservation.Status == "Pending" || reservation.Status == "Approved"))
            .Select(reservation => new { reservation.ReservationDate, reservation.ReservationTime })
            .ToListAsync();
        var reservationsByDate = reservations
            .GroupBy(reservation => reservation.ReservationDate)
            .ToDictionary(group => group.Key, group => group.Select(reservation => reservation.ReservationTime).ToArray());

        var days = Enumerable.Range(1, DateTime.DaysInMonth(firstDay.Year, firstDay.Month))
            .Select(day => new
            {
                date = FormatDate(new DateOnly(firstDay.Year, firstDay.Month, day)),
                isAvailable = !unavailableDates.Contains(
                    FormatDate(new DateOnly(firstDay.Year, firstDay.Month, day))),
                reservedTimes = reservationsByDate.GetValueOrDefault(
                    FormatDate(new DateOnly(firstDay.Year, firstDay.Month, day))) ?? [],
                reservationCount = reservationsByDate.GetValueOrDefault(
                    FormatDate(new DateOnly(firstDay.Year, firstDay.Month, day)))?.Length ?? 0
            });

        return Ok(new { month, dailyCapacity = DailyCapacity, days });
    }

    [HttpPost("availability")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetAvailable([FromBody] AvailabilityRequest request)
    {
        if (!TryParseDate(request.Date, out var date) || date <= DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new { message = "Choose a future date." });
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _context.UnavailableDates
            .Where(unavailableDate => unavailableDate.Date == dateText)
            .ExecuteDeleteAsync();

        return Ok(new { date = dateText, isAvailable = true });
    }

    [HttpDelete("availability/{date}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetUnavailable(string date)
    {
        if (!TryParseDate(date, out var parsedDate) || parsedDate <= DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new { message = "Choose a future date." });
        }

        var dateText = parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await _context.Reservations.AnyAsync(reservation =>
                reservation.ReservationDate == dateText &&
                (reservation.Status == "Pending" || reservation.Status == "Approved")))
        {
            return Conflict(new { message = "A date with pending or approved reservations cannot be closed." });
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT OR IGNORE INTO \"UnavailableDates\" (\"Date\") VALUES ({dateText})");
        return NoContent();
    }

    [HttpPost("reservations")]
    [Authorize(Roles = AppRoles.Customer)]
    public async Task<IActionResult> CreateReservation([FromBody] ReservationRequest request)
    {
        if (!TryParseDate(request.Date, out var date) || date <= DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new { message = "Choose a future date." });
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var customer = await _users.GetUserAsync(User);
        if (customer is null || !customer.EmailConfirmed)
        {
            return Unauthorized(new { message = "Sign in with a confirmed customer account to reserve a date." });
        }

        var city = request.City.Trim();
        var locationAddress = request.LocationAddress.Trim();
        var serviceType = request.ServiceType;
        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Reservations" ("CustomerId", "ReservationDate", "ReservationTime", "CustomerName", "Email", "CustomerPhone", "City", "LocationAddress", "ServiceType", "TravelFeeCents", "Status", "RejectionReason", "CreatedAtUtc")
            SELECT {customer.Id}, {dateText}, {request.Time}, {customer.DisplayName}, {customer.Email!}, {customer.PhoneNumber ?? string.Empty}, {city}, {locationAddress}, {serviceType}, 0, 'Pending', '', {DateTime.UtcNow}
            WHERE NOT EXISTS (SELECT 1 FROM "UnavailableDates" WHERE "Date" = {dateText})
                AND (SELECT COUNT(*) FROM "Reservations" WHERE "ReservationDate" = {dateText} AND ("Status" = 'Pending' OR "Status" = 'Approved')) < {DailyCapacity}
                AND NOT EXISTS (SELECT 1 FROM "Reservations" WHERE "ReservationDate" = {dateText} AND "ReservationTime" = {request.Time} AND ("Status" = 'Pending' OR "Status" = 'Approved'))
                AND NOT EXISTS (SELECT 1 FROM "Reservations" WHERE "CustomerId" = {customer.Id} AND "ReservationDate" = {dateText} AND ("Status" = 'Pending' OR "Status" = 'Approved'))
            """);

        if (affectedRows == 0)
        {
            if (await _context.UnavailableDates.AnyAsync(unavailableDate => unavailableDate.Date == dateText))
            {
                return Conflict(new { message = "This date is not available for reservations." });
            }

            if (await _context.Reservations.AnyAsync(reservation =>
                    reservation.CustomerId == customer.Id &&
                    reservation.ReservationDate == dateText &&
                    (reservation.Status == "Pending" || reservation.Status == "Approved")))
            {
                return Conflict(new { message = "You already have a pending or approved reservation on this date." });
            }

            if (await _context.Reservations.AnyAsync(reservation =>
                    reservation.ReservationDate == dateText &&
                    reservation.ReservationTime == request.Time &&
                    reservation.Status != "Rejected"))
            {
                return Conflict(new { message = "That time is no longer available." });
            }

            return Conflict(new { message = "This date is fully booked." });
        }

        await NotifyAdminsOfNewReservationAsync(
            customer.DisplayName,
            customer.Email!,
            customer.PhoneNumber ?? string.Empty,
            dateText,
            request.Time,
            serviceType,
            city,
            locationAddress);

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Your reservation is pending admin approval."
        });
    }

    private async Task NotifyAdminsOfNewReservationAsync(
        string customerName,
        string customerEmail,
        string customerPhone,
        string date,
        string time,
        string serviceType,
        string city,
        string address)
    {
        if (!_emailSender.IsConfigured)
        {
            _logger.LogWarning("Could not email admins about reservation on {Date}: SMTP is not configured.", date);
            return;
        }

        var recipients = (await _users.GetUsersInRoleAsync(AppRoles.Admin))
            .Select(admin => admin.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (recipients.Length == 0)
        {
            _logger.LogWarning("Could not email admins about reservation on {Date}: no admin email addresses are configured.", date);
            return;
        }

        foreach (var recipient in recipients)
        {
            try
            {
                await _emailSender.SendBookingNotificationAsync(
                    recipient!,
                    customerName,
                    customerEmail,
                    customerPhone,
                    date,
                    time,
                    serviceType,
                    city,
                    address);
            }
            catch (SmtpException exception)
            {
                _logger.LogError(exception, "Failed to email admin {AdminEmail} about reservation on {Date}.", recipient, date);
            }
        }
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public class AvailabilityRequest
{
    [Required]
    public string Date { get; set; } = string.Empty;
}

public class ReservationRequest
{
    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
    public string Date { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^(09|1[0-6]):00$")]
    public string Time { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Home|Workshop)$")]
    public string ServiceType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 5)]
    public string LocationAddress { get; set; } = string.Empty;
}
