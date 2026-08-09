using HabitTracker.Application.Reminders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HabitTracker.Infrastructure.Services;

public class ReminderSchedulerOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Off by default so tests and one-off runs never send mail unbidden.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How often to look for due reminders. Users pick an hour, not a minute, so a
    /// few minutes of granularity is plenty and keeps the query load low.
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Periodically asks the application layer to deliver whatever is due. All the
/// timing rules live in ReminderPlanner; this class only supplies the heartbeat.
/// </summary>
public class ReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderSchedulerOptions> options,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    private readonly ReminderSchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Reminder scheduler disabled; not polling.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(_options.PollIntervalMinutes, 1, 60));
        logger.LogInformation("Reminder scheduler polling every {Minutes} minute(s).", interval.TotalMinutes);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                var sent = await reminders.ProcessDueRemindersAsync(stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Sent {Count} reminder email(s).", sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                // Never let one bad tick kill the loop; the next tick retries.
                logger.LogError(e, "Reminder sweep failed.");
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
