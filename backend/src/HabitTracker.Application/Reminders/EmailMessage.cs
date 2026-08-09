namespace HabitTracker.Application.Reminders;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>
/// Delivery transport. Behind an interface so reminders can be exercised in tests
/// and run in development without an SMTP server.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
