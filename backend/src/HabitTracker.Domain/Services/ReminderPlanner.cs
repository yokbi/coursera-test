using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Services;

/// <summary>
/// Pure scheduling rules for reminder mail. Everything here works on the user's
/// own local clock; converting UTC to that clock happens at the edges.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>
    /// A reminder goes out once per local day, at or after the user's chosen hour,
    /// and only while something is still pending.
    /// </summary>
    /// <param name="remindersEnabled">User's opt-in flag.</param>
    /// <param name="reminderHour">Preferred local hour, 0-23.</param>
    /// <param name="lastSentOn">Local day the previous reminder was sent, if any.</param>
    /// <param name="localNow">Current wall-clock time in the user's timezone.</param>
    /// <param name="pendingHabitCount">Habits still incomplete for that local day.</param>
    public static bool ShouldSend(
        bool remindersEnabled,
        int reminderHour,
        DateOnly? lastSentOn,
        DateTime localNow,
        int pendingHabitCount)
    {
        if (!remindersEnabled || pendingHabitCount <= 0)
        {
            return false;
        }

        var localDate = DateOnly.FromDateTime(localNow);
        if (lastSentOn == localDate)
        {
            return false;
        }

        // Late is better than never: a scheduler outage still delivers later the
        // same local day, and the day boundary stops it from leaking into the night.
        return localNow.Hour >= Math.Clamp(reminderHour, 0, 23);
    }

    /// <summary>
    /// Whether a habit still needs attention on the given local day. Weekly-quota
    /// habits are judged by the week's progress, not by that single day.
    /// </summary>
    public static bool IsPending(
        ScheduleType scheduleType,
        Weekdays scheduleDays,
        int? timesPerWeek,
        decimal completionThreshold,
        decimal todayValue,
        int weekCompletions,
        DateOnly localDate)
    {
        switch (scheduleType)
        {
            case ScheduleType.SpecificWeekdays when !scheduleDays.Includes(localDate):
                return false;

            case ScheduleType.TimesPerWeek:
                // Nagging daily about a 2x/week habit is noise; only the unmet quota matters.
                return weekCompletions < Math.Max(1, timesPerWeek ?? 1);

            default:
                return todayValue < completionThreshold;
        }
    }
}
