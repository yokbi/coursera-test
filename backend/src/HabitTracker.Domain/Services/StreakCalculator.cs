using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Services;

public sealed record StreakResult(int Current, int Longest, string Unit)
{
    public static readonly StreakResult Empty = new(0, 0, "days");
}

/// <summary>
/// Pure, timezone-agnostic streak engine. All dates passed in must already be
/// calendar days in the habit owner's timezone; UTC conversion happens at the edges.
/// </summary>
public static class StreakCalculator
{
    /// <param name="scheduleType">Habit schedule variant.</param>
    /// <param name="scheduleDays">Scheduled weekdays (SpecificWeekdays only).</param>
    /// <param name="timesPerWeek">Weekly quota (TimesPerWeek only).</param>
    /// <param name="completedDates">Local calendar days on which the habit reached its target.</param>
    /// <param name="today">Today's local calendar day for the habit owner.</param>
    /// <param name="startDate">The habit's first local day; streaks never look further back.</param>
    public static StreakResult Calculate(
        ScheduleType scheduleType,
        Weekdays scheduleDays,
        int? timesPerWeek,
        IReadOnlyCollection<DateOnly> completedDates,
        DateOnly today,
        DateOnly startDate)
    {
        if (startDate > today)
        {
            return scheduleType == ScheduleType.TimesPerWeek
                ? StreakResult.Empty with { Unit = "weeks" }
                : StreakResult.Empty;
        }

        var completed = completedDates as IReadOnlySet<DateOnly> ?? completedDates.ToHashSet();
        return scheduleType switch
        {
            ScheduleType.TimesPerWeek => CalculateWeekly(timesPerWeek ?? 1, completed, today, startDate),
            ScheduleType.SpecificWeekdays => CalculateDayBased(d => scheduleDays.Includes(d), completed, today, startDate),
            _ => CalculateDayBased(_ => true, completed, today, startDate)
        };
    }

    private static StreakResult CalculateDayBased(
        Func<DateOnly, bool> isScheduled,
        IReadOnlySet<DateOnly> completed,
        DateOnly today,
        DateOnly startDate)
    {
        // Current streak: walk back from today over scheduled days.
        // Today, while still in progress, never breaks the streak — it only extends it once completed.
        var current = 0;
        var day = today;
        var isFirstScheduledDay = true;
        while (day >= startDate)
        {
            if (isScheduled(day))
            {
                if (completed.Contains(day))
                {
                    current++;
                }
                else if (!(isFirstScheduledDay && day == today))
                {
                    break;
                }

                isFirstScheduledDay = false;
            }

            day = day.AddDays(-1);
        }

        // Longest streak: single forward sweep over the whole habit lifetime.
        var longest = current;
        var run = 0;
        for (var d = startDate; d <= today; d = d.AddDays(1))
        {
            if (!isScheduled(d))
            {
                continue;
            }

            if (completed.Contains(d))
            {
                run++;
                longest = Math.Max(longest, run);
            }
            else if (d != today)
            {
                run = 0;
            }
        }

        return new StreakResult(current, longest, "days");
    }

    private static StreakResult CalculateWeekly(
        int target,
        IReadOnlySet<DateOnly> completed,
        DateOnly today,
        DateOnly startDate)
    {
        target = Math.Max(1, target);
        var completionsPerWeek = completed
            .Where(d => d >= startDate && d <= today)
            .GroupBy(WeekStart)
            .ToDictionary(g => g.Key, g => g.Count());

        var currentWeek = WeekStart(today);
        var firstWeek = WeekStart(startDate);

        // Current streak: the in-progress week counts if already met, otherwise it is skipped without breaking.
        var current = 0;
        var week = currentWeek;
        if (completionsPerWeek.GetValueOrDefault(week) >= target)
        {
            current++;
        }

        week = week.AddDays(-7);
        while (week >= firstWeek && completionsPerWeek.GetValueOrDefault(week) >= target)
        {
            current++;
            week = week.AddDays(-7);
        }

        // Longest: sweep completed weeks; the in-progress current week only counts once met.
        var longest = current;
        var run = 0;
        for (var w = firstWeek; w <= currentWeek; w = w.AddDays(7))
        {
            if (completionsPerWeek.GetValueOrDefault(w) >= target)
            {
                run++;
                longest = Math.Max(longest, run);
            }
            else if (w != currentWeek)
            {
                run = 0;
            }
        }

        return new StreakResult(current, longest, "weeks");
    }

    /// <summary>Monday of the ISO week containing the given date.</summary>
    public static DateOnly WeekStart(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
        return date.AddDays(-offset);
    }
}
