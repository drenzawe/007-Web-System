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

        using var client = new SmtpClient(
            configuration["Email:Smtp:Host"]!,
            int.Parse(configuration["Email:Smtp:Port"]!))
        {
            EnableSsl = !bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var enableSsl) || enableSsl,
            Credentials = new NetworkCredential(
                configuration["Email:Smtp:Username"],
                configuration["Email:Smtp:Password"])
        };

        await client.SendMailAsync(message);
    }
}
