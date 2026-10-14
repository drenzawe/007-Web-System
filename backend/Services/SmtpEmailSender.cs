using System.Net;
using System.Net.Mail;

namespace backend.Services;

public sealed class SmtpEmailSender(IConfiguration configuration)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]) &&
        int.TryParse(configuration["Email:Smtp:Port"], out _) &&
        !string.IsNullOrWhiteSpace(configuration["Email:From"]) &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Username"]) &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Password"]);

    public async Task SendConfirmationAsync(string recipient, string confirmationUrl)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Email verification is not configured. Set the SMTP host, port, username, password, and sender address.");
        }

        var message = new MailMessage(
            configuration["Email:From"]!,
            recipient,
            "Confirm your 007 Headlight Retrofit account",
            $"""
            <p>Welcome to 007 Headlight Retrofit.</p>
            <p><a href="{WebUtility.HtmlEncode(confirmationUrl)}">Confirm your email address</a> to activate your account.</p>
            """)
        {
            IsBodyHtml = true
        };

        using var client = CreateClient();
        await client.SendMailAsync(message);
    }

    public async Task SendBookingNotificationAsync(
        string recipient,
        string customerName,
        string customerEmail,
        string customerPhone,
        string date,
        string time,
        string serviceType,
        string city,
        string address)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Booking email notifications are not configured. Set the SMTP host, port, username, password, and sender address.");
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";
        var adminUrl = WebUtility.HtmlEncode($"{frontendBaseUrl}/admin");
        var message = new MailMessage(
            configuration["Email:From"]!,
            recipient,
            $"New reservation request for {date}",
            $"""
            <p>A new reservation request was received.</p>
            <p><strong>Customer:</strong> {WebUtility.HtmlEncode(customerName)}<br>
            <strong>Email:</strong> {WebUtility.HtmlEncode(customerEmail)}<br>
            <strong>Phone:</strong> {WebUtility.HtmlEncode(customerPhone)}<br>
            <strong>Date and time:</strong> {WebUtility.HtmlEncode(date)} at {WebUtility.HtmlEncode(time)}<br>
            <strong>Service:</strong> {WebUtility.HtmlEncode(serviceType)}<br>
            <strong>City:</strong> {WebUtility.HtmlEncode(city)}<br>
            <strong>Address:</strong> {WebUtility.HtmlEncode(address)}</p>
            <p><a href="{adminUrl}">Review this request in the admin panel</a></p>
            """)
        {
            IsBodyHtml = true
        };

        using var client = CreateClient();
        await client.SendMailAsync(message);
    }

    public async Task SendReservationStatusAsync(
        string recipient,
        string customerName,
        string date,
        string time,
        string serviceType,
        string status,
        string rejectionReason,
        int travelFeeCents)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Reservation status emails are not configured. Set the SMTP host, port, username, password, and sender address.");
        }

        var isRejected = status == "Rejected";
        var details = isRejected
            ? $"<p><strong>Reason:</strong> {WebUtility.HtmlEncode(rejectionReason)}</p>"
            : serviceType == "Home" && travelFeeCents > 0
                ? $"<p><strong>Home-service travel fee:</strong> PHP {(travelFeeCents / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}</p>"
                : string.Empty;
        var message = new MailMessage(
            configuration["Email:From"]!,
            recipient,
            $"Your reservation has been {status.ToLowerInvariant()}",
            $"""
            <p>Hello {WebUtility.HtmlEncode(customerName)},</p>
            <p>Your reservation for <strong>{WebUtility.HtmlEncode(date)}</strong> at <strong>{WebUtility.HtmlEncode(time)}</strong> has been <strong>{WebUtility.HtmlEncode(status.ToLowerInvariant())}</strong>.</p>
            <p><strong>Service:</strong> {WebUtility.HtmlEncode(serviceType == "Home" ? "Home service" : "Workshop visit")}</p>
            {details}
            <p>You can view your reservations by signing in to 007 Headlight Retrofit.</p>
            """)
        {
            IsBodyHtml = true
        };

        using var client = CreateClient();
        await client.SendMailAsync(message);
    }

    private SmtpClient CreateClient() =>
        new(configuration["Email:Smtp:Host"]!, int.Parse(configuration["Email:Smtp:Port"]!))
        {
            EnableSsl = !bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var enableSsl) || enableSsl,
            Credentials = new NetworkCredential(
                configuration["Email:Smtp:Username"],
                configuration["Email:Smtp:Password"])
        };
}
