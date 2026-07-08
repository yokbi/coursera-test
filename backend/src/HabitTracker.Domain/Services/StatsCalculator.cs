using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Services;

public sealed record HeatmapDay(DateOnly Date, decimal Value, bool Completed, bool Scheduled);

public static class StatsCalculator
{
    /// <summary>
    /// Completion rate in [0,1] over the window [from, to] (local days, inclusive).
    /// Day-based schedules: completed scheduled days / scheduled days.
    /// TimesPerWeek: average weekly credit, each week worth min(completions, target) / target.
    /// </summary>
    public static double CompletionRate(
        ScheduleType scheduleType,
        Weekdays scheduleDays,
        int? timesPerWeek,
        IReadOnlyCollection<DateOnly> completedDates,
        DateOnly from,
        DateOnly to,
        DateOnly startDate)
    {
        if (from < startDate)
        {
            from = startDate;
        }

        if (from > to)
        {
            return 0;
        }

        var completed = completedDates as IReadOnlySet<DateOnly> ?? completedDates.ToHashSet();

        if (scheduleType == ScheduleType.TimesPerWeek)
        {
            var target = Math.Max(1, timesPerWeek ?? 1);
            var weeks = 0;
            double credit = 0;
            for (var w = StreakCalculator.WeekStart(from); w <= to; w = w.AddDays(7))
            {
                weeks++;
                var count = 0;
                for (var d = w; d < w.AddDays(7); d = d.AddDays(1))
                {
                    if (d >= from && d <= to && completed.Contains(d))
                    {
                        count++;
                    }
                }

                credit += Math.Min(count, target) / (double)target;
            }

            return weeks == 0 ? 0 : credit / weeks;
        }

        var scheduled = 0;
        var done = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var isScheduled = scheduleType != ScheduleType.SpecificWeekdays || scheduleDays.Includes(d);
            if (!isScheduled)
            {
                continue;
            }

            scheduled++;
            if (completed.Contains(d))
            {
                done++;
            }
        }

        return scheduled == 0 ? 0 : done / (double)scheduled;
    }

    /// <summary>Per-day heatmap cells for the window [from, to], oldest first.</summary>
    public static IReadOnlyList<HeatmapDay> Heatmap(
        ScheduleType scheduleType,
        Weekdays scheduleDays,
        decimal completionThreshold,
        IReadOnlyDictionary<DateOnly, decimal> valuesByDate,
        DateOnly from,
        DateOnly to)
    {
        var result = new List<HeatmapDay>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var value = valuesByDate.GetValueOrDefault(d);
            var scheduled = scheduleType switch
            {
                ScheduleType.SpecificWeekdays => scheduleDays.Includes(d),
                _ => true
            };
            result.Add(new HeatmapDay(d, value, value >= completionThreshold && value > 0, scheduled));
        }

        return result;
    }
}
