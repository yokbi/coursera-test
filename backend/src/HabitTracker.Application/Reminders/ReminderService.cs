using HabitTracker.Application.Auth;
using HabitTracker.Application.CheckIns;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HabitTracker.Application.Reminders;

public sealed record ReminderSettingsRequest(bool Enabled, int Hour);

/// <summary>Signed, single-purpose link so unsubscribing from a mail needs no login.</summary>
public interface IUnsubscribeTokenService
{
    /// <summary>Absolute URL the recipient can click, including the signed token.</summary>
    string CreateUnsubscribeUrl(Guid userId);
    Guid? Validate(string token);
}

public interface IReminderService
{
    /// <summary>Sends every reminder that is due right now. Returns how many went out.</summary>
    Task<int> ProcessDueRemindersAsync(CancellationToken ct = default);
    Task<UserDto> UpdateSettingsAsync(Guid userId, ReminderSettingsRequest request, CancellationToken ct = default);
    Task<bool> UnsubscribeAsync(string token, CancellationToken ct = default);
}

public class ReminderService(
    IAppDbContext db,
    IClock clock,
    IEmailSender emailSender,
    IUnsubscribeTokenService unsubscribeTokens,
    ILogger<ReminderService> logger) : IReminderService
{
    /// <summary>Where the unsubscribe link points; the API serves this route.</summary>
    public const string UnsubscribePath = "/api/v1/reminders/unsubscribe";

    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var utcNow = clock.UtcNow;
        var candidates = await db.Users
            .Where(u => u.RemindersEnabled && !u.IsDeleted)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var user in candidates)
        {
            try
            {
                if (await TrySendAsync(user, utcNow, ct))
                {
                    sent++;
                }
            }
            catch (Exception e)
            {
                // One bad recipient (unknown timezone, transport hiccup) must not
                // stop the rest of the batch. No address or content is logged.
                logger.LogError(e, "Reminder delivery failed for user {UserId}", user.Id);
            }
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return sent;
    }

    private async Task<bool> TrySendAsync(User user, DateTime utcNow, CancellationToken ct)
    {
        if (!TimezoneHelper.IsValidTimeZone(user.TimeZone))
        {
            logger.LogWarning("Skipping reminder for user {UserId}: unknown timezone", user.Id);
            return false;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);
        var localDate = DateOnly.FromDateTime(localNow);

        // Cheap gates first: no habit queries for users whose hour has not arrived.
        if (user.LastReminderSentOn == localDate ||
            localNow.Hour < Math.Clamp(user.ReminderHour, 0, 23))
        {
            return false;
        }

        var pending = await CollectPendingAsync(user, localDate, ct);
        if (!ReminderPlanner.ShouldSend(
                user.RemindersEnabled, user.ReminderHour, user.LastReminderSentOn, localNow, pending.Count))
        {
            return false;
        }

        var message = ReminderContentBuilder.Build(
            user.Email, pending, unsubscribeTokens.CreateUnsubscribeUrl(user.Id));
        await emailSender.SendAsync(message, ct);

        // Marked only after a successful send, so a transport failure retries next tick.
        user.LastReminderSentOn = localDate;
        return true;
    }

    private async Task<List<PendingHabit>> CollectPendingAsync(User user, DateOnly localDate, CancellationToken ct)
    {
        var habits = await db.Habits.AsNoTracking()
            .Where(h => h.UserId == user.Id && !h.IsArchived)
            .OrderBy(h => h.SortOrder)
            .ToListAsync(ct);

        var weekStart = StreakCalculator.WeekStart(localDate);
        var pending = new List<PendingHabit>();

        foreach (var habit in habits)
        {
            var threshold = habit.CompletionThreshold;
            var todayValue = await db.CheckIns.AsNoTracking()
                .Where(c => c.HabitId == habit.Id && c.Date == localDate)
                .Select(c => (decimal?)c.Value)
                .FirstOrDefaultAsync(ct) ?? 0;

            var weekCompletions = habit.ScheduleType == Domain.Enums.ScheduleType.TimesPerWeek
                ? await db.CheckIns.AsNoTracking()
                    .CountAsync(c => c.HabitId == habit.Id
                                     && c.Date >= weekStart && c.Date <= localDate
                                     && c.Value >= threshold && c.Value > 0, ct)
                : 0;

            if (!ReminderPlanner.IsPending(
                    habit.ScheduleType, habit.ScheduleDays, habit.TimesPerWeek,
                    threshold, todayValue, weekCompletions, localDate))
            {
                continue;
            }

            var streak = await CheckInService.ComputeStreakAsync(db, habit, localDate, ct);
            pending.Add(new PendingHabit(habit.Name, habit.Icon, streak.Current, streak.Unit));
        }

        return pending;
    }

    public async Task<UserDto> UpdateSettingsAsync(Guid userId, ReminderSettingsRequest request, CancellationToken ct = default)
    {
        if (request.Hour is < 0 or > 23)
        {
            throw new FluentValidation.ValidationException("Hour must be between 0 and 23.",
                [new FluentValidation.Results.ValidationFailure("hour", "Hour must be between 0 and 23.")]);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedAppException("Account is no longer active.");

        user.RemindersEnabled = request.Enabled;
        user.ReminderHour = request.Hour;
        // Re-enabling mid-day should not be blocked by an earlier send marker.
        if (!request.Enabled)
        {
            user.LastReminderSentOn = null;
        }

        await db.SaveChangesAsync(ct);
        return UserMapper.ToDto(user);
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken ct = default)
    {
        if (unsubscribeTokens.Validate(token) is not { } userId)
        {
            return false;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
        {
            return false;
        }

        user.RemindersEnabled = false;
        user.LastReminderSentOn = null;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
