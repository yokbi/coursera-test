using System.Net;
using System.Net.Mail;
using HabitTracker.Application.Reminders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HabitTracker.Infrastructure.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Public origin of this API; unsubscribe links in mail point back here.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public string FromAddress { get; set; } = "hatirlatma@habittracker.local";
    public string FromName { get; set; } = "Alışkanlık Takibi";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}

/// <summary>
/// Development fallback: records that a mail would have gone out without needing a
/// mail server. Subject and recipient domain only — no address, no body.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var at = message.To.IndexOf('@');
        var domain = at >= 0 ? message.To[at..] : "(none)";
        logger.LogInformation("Reminder email suppressed (no SMTP configured): subject {Subject} to {Domain}",
            message.Subject, domain);
        return Task.CompletedTask;
    }
}

public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.TextBody, null, "text/plain"));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            Credentials = string.IsNullOrEmpty(_options.SmtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
        };

        await client.SendMailAsync(mail, ct);
    }
}
