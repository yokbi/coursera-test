using HabitTracker.Domain.Enums;
using HabitTracker.Domain.Services;

namespace HabitTracker.UnitTests.Domain;

public class StatsCalculatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void CompletionRate_Daily_AllDone_Is100()
    {
        var completed = Enumerable.Range(0, 7).Select(i => D(2026, 7, 1).AddDays(i)).ToList();
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.Daily, Weekdays.None, null, completed, D(2026, 7, 1), D(2026, 7, 7), D(2026, 7, 1));
        Assert.Equal(1.0, rate, 3);
    }

    [Fact]
    public void CompletionRate_Daily_HalfDone()
    {
        var completed = new[] { D(2026, 7, 1), D(2026, 7, 3) };
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.Daily, Weekdays.None, null, completed, D(2026, 7, 1), D(2026, 7, 4), D(2026, 7, 1));
        Assert.Equal(0.5, rate, 3);
    }

    [Fact]
    public void CompletionRate_Weekdays_OnlyScheduledDaysCount()
    {
        // Mon/Wed in the week of 2026-07-06: scheduled = Jul 6 (Mon), Jul 8 (Wed).
        var completed = new[] { D(2026, 7, 6) };
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.SpecificWeekdays, Weekdays.Monday | Weekdays.Wednesday, null,
            completed, D(2026, 7, 6), D(2026, 7, 12), D(2026, 7, 1));
        Assert.Equal(0.5, rate, 3);
    }

    [Fact]
    public void CompletionRate_WindowClampedToStartDate()
    {
        var completed = new[] { D(2026, 7, 5), D(2026, 7, 6) };
        // Window starts Jul 1 but habit starts Jul 5 → denominator is 2 days, both done.
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.Daily, Weekdays.None, null, completed, D(2026, 7, 1), D(2026, 7, 6), D(2026, 7, 5));
        Assert.Equal(1.0, rate, 3);
    }

    [Fact]
    public void CompletionRate_TimesPerWeek_AveragesWeeklyCredit()
    {
        // Target 2/week over two full weeks: week 1 has 2 (full credit), week 2 has 1 (half credit).
        var completed = new[] { D(2026, 6, 29), D(2026, 6, 30), D(2026, 7, 6) };
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.TimesPerWeek, Weekdays.None, 2, completed, D(2026, 6, 29), D(2026, 7, 12), D(2026, 6, 29));
        Assert.Equal(0.75, rate, 3);
    }

    [Fact]
    public void CompletionRate_EmptyWindow_ReturnsZero()
    {
        var rate = StatsCalculator.CompletionRate(
            ScheduleType.Daily, Weekdays.None, null, [], D(2026, 7, 8), D(2026, 7, 1), D(2026, 7, 1));
        Assert.Equal(0, rate);
    }

    [Fact]
    public void Heatmap_MarksCompletedScheduledAndValues()
    {
        var values = new Dictionary<DateOnly, decimal>
        {
            [D(2026, 7, 6)] = 8, // meets threshold
            [D(2026, 7, 7)] = 3  // below threshold
        };
        var map = StatsCalculator.Heatmap(
            ScheduleType.SpecificWeekdays, Weekdays.Monday | Weekdays.Tuesday, 8m,
            values, D(2026, 7, 6), D(2026, 7, 8));

        Assert.Equal(3, map.Count);
        Assert.True(map[0].Completed);
        Assert.True(map[0].Scheduled);
        Assert.False(map[1].Completed);
        Assert.Equal(3m, map[1].Value);
        Assert.False(map[2].Scheduled); // Wednesday not scheduled
        Assert.Equal(0m, map[2].Value);
    }

    [Fact]
    public void Heatmap_ZeroValueNeverCompleted_EvenWithZeroThreshold()
    {
        var map = StatsCalculator.Heatmap(
            ScheduleType.Daily, Weekdays.None, 0m,
            new Dictionary<DateOnly, decimal>(), D(2026, 7, 8), D(2026, 7, 8));
        Assert.False(map[0].Completed);
    }
}
