using HabitTracker.Domain.Enums;
using HabitTracker.Domain.Services;

namespace HabitTracker.UnitTests.Domain;

public class StreakCalculatorTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static StreakResult Daily(IEnumerable<DateOnly> completed, DateOnly today, DateOnly start) =>
        StreakCalculator.Calculate(ScheduleType.Daily, Weekdays.None, null, completed.ToList(), today, start);

    private static StreakResult OnDays(Weekdays days, IEnumerable<DateOnly> completed, DateOnly today, DateOnly start) =>
        StreakCalculator.Calculate(ScheduleType.SpecificWeekdays, days, null, completed.ToList(), today, start);

    private static StreakResult Weekly(int target, IEnumerable<DateOnly> completed, DateOnly today, DateOnly start) =>
        StreakCalculator.Calculate(ScheduleType.TimesPerWeek, Weekdays.None, target, completed.ToList(), today, start);

    // --- Daily ---

    [Fact]
    public void Daily_NoCheckIns_ZeroStreaks()
    {
        var result = Daily([], D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
        Assert.Equal("days", result.Unit);
    }

    [Fact]
    public void Daily_ConsecutiveDaysEndingToday_CountsAll()
    {
        var completed = new[] { D(2026, 7, 6), D(2026, 7, 7), D(2026, 7, 8) };
        var result = Daily(completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(3, result.Current);
        Assert.Equal(3, result.Longest);
    }

    [Fact]
    public void Daily_TodayNotYetDone_DoesNotBreakStreak()
    {
        var completed = new[] { D(2026, 7, 6), D(2026, 7, 7) };
        var result = Daily(completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(2, result.Current);
    }

    [Fact]
    public void Daily_YesterdayMissed_CurrentIsZero_ButTodayDoneCountsOne()
    {
        var completed = new[] { D(2026, 7, 5), D(2026, 7, 6), D(2026, 7, 8) };
        var result = Daily(completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(1, result.Current);
        Assert.Equal(2, result.Longest);
    }

    [Fact]
    public void Daily_GapInHistory_LongestReflectsBestRun()
    {
        var completed = new[]
        {
            D(2026, 6, 1), D(2026, 6, 2), D(2026, 6, 3), D(2026, 6, 4), // run of 4
            D(2026, 6, 10), D(2026, 6, 11), // run of 2
            D(2026, 7, 8) // today
        };
        var result = Daily(completed, D(2026, 7, 8), D(2026, 6, 1));
        Assert.Equal(1, result.Current);
        Assert.Equal(4, result.Longest);
    }

    [Fact]
    public void Daily_BackfilledCheckIn_RepairsStreak()
    {
        // 7th was missing, then backfilled: 6,7,8 now continuous.
        var completed = new[] { D(2026, 7, 6), D(2026, 7, 7), D(2026, 7, 8) };
        var result = Daily(completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(3, result.Current);
    }

    [Fact]
    public void Daily_StreakDoesNotExtendBeforeStartDate()
    {
        // Completions exist before the habit start; they must be ignored.
        var completed = new[] { D(2026, 7, 1), D(2026, 7, 2), D(2026, 7, 3), D(2026, 7, 4) };
        var result = Daily(completed, D(2026, 7, 4), D(2026, 7, 3));
        Assert.Equal(2, result.Current);
        Assert.Equal(2, result.Longest);
    }

    [Fact]
    public void Daily_StartDateInFuture_ReturnsEmpty()
    {
        var result = Daily([D(2026, 7, 8)], D(2026, 7, 8), D(2026, 7, 10));
        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
    }

    [Fact]
    public void Daily_SingleDayHabit_CreatedAndDoneToday()
    {
        var result = Daily([D(2026, 7, 8)], D(2026, 7, 8), D(2026, 7, 8));
        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
    }

    // --- Specific weekdays ---

    [Fact]
    public void Weekdays_UnscheduledDaysDoNotBreakStreak()
    {
        // Mon/Wed/Fri habit. 2026-07-06 is Monday, 2026-07-08 is Wednesday.
        var days = Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday;
        var completed = new[] { D(2026, 7, 3), D(2026, 7, 6), D(2026, 7, 8) }; // Fri, Mon, Wed
        var result = OnDays(days, completed, D(2026, 7, 9), D(2026, 7, 1)); // Thursday (unscheduled)
        Assert.Equal(3, result.Current);
        Assert.Equal(3, result.Longest);
    }

    [Fact]
    public void Weekdays_MissedScheduledDay_BreaksStreak()
    {
        var days = Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday;
        // Missed Monday 07-06, completed Wednesday 07-08.
        var completed = new[] { D(2026, 7, 3), D(2026, 7, 8) };
        var result = OnDays(days, completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
    }

    [Fact]
    public void Weekdays_TodayScheduledButPending_DoesNotBreak()
    {
        var days = Weekdays.Monday | Weekdays.Wednesday;
        var completed = new[] { D(2026, 7, 6) }; // Monday done, Wednesday (today) pending
        var result = OnDays(days, completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(1, result.Current);
    }

    [Fact]
    public void Weekdays_CompletionOnUnscheduledDay_DoesNotCount()
    {
        var days = Weekdays.Monday;
        var completed = new[] { D(2026, 7, 7) }; // Tuesday — not scheduled
        var result = OnDays(days, completed, D(2026, 7, 8), D(2026, 7, 1));
        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
    }

    [Fact]
    public void Weekdays_HabitCreatedMidWeek_EarlierScheduledDaysIgnored()
    {
        // Mon/Wed/Fri habit created on Thursday 07-02; Friday 07-03 completed.
        var days = Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday;
        var completed = new[] { D(2026, 7, 3) };
        var result = OnDays(days, completed, D(2026, 7, 4), D(2026, 7, 2));
        Assert.Equal(1, result.Current);
    }

    // --- Times per week ---

    [Fact]
    public void Weekly_TargetMetConsecutiveWeeks_CountsWeeks()
    {
        // Target 2/week. Weeks of Jun 22 and Jun 29 met; current week (Jul 6) met.
        var completed = new[]
        {
            D(2026, 6, 22), D(2026, 6, 25),
            D(2026, 6, 30), D(2026, 7, 2),
            D(2026, 7, 6), D(2026, 7, 7)
        };
        var result = Weekly(2, completed, D(2026, 7, 8), D(2026, 6, 22));
        Assert.Equal(3, result.Current);
        Assert.Equal(3, result.Longest);
        Assert.Equal("weeks", result.Unit);
    }

    [Fact]
    public void Weekly_CurrentWeekInProgressBelowTarget_DoesNotBreak()
    {
        var completed = new[]
        {
            D(2026, 6, 29), D(2026, 6, 30), D(2026, 7, 1), // last week: 3 ≥ 3
            D(2026, 7, 6) // current week: only 1 so far
        };
        var result = Weekly(3, completed, D(2026, 7, 8), D(2026, 6, 29));
        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
    }

    [Fact]
    public void Weekly_MissedWeek_BreaksStreak()
    {
        var completed = new[]
        {
            D(2026, 6, 15), D(2026, 6, 16), // week met
            // week of Jun 22 missed entirely
            D(2026, 6, 29), D(2026, 6, 30) // week met
        };
        var result = Weekly(2, completed, D(2026, 7, 8), D(2026, 6, 15));
        // Current week is empty but in progress; walk-back reaches the met Jun 29 week,
        // then stops at the missed Jun 22 week → the Jun 15 week does not chain.
        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
    }

    [Fact]
    public void Weekly_PriorWeekMet_CurrentWeekEmpty_StreakHolds()
    {
        var completed = new[] { D(2026, 6, 29), D(2026, 6, 30) };
        var result = Weekly(2, completed, D(2026, 7, 8), D(2026, 6, 29));
        Assert.Equal(1, result.Current);
    }

    [Fact]
    public void Weekly_ExcessCompletionsInOneWeek_DoNotCarryOver()
    {
        var completed = new[]
        {
            D(2026, 6, 29), D(2026, 6, 30), D(2026, 7, 1), D(2026, 7, 2) // 4 in one week, target 2
        };
        var result = Weekly(2, completed, D(2026, 7, 8), D(2026, 6, 29));
        Assert.Equal(1, result.Current);
        Assert.Equal(1, result.Longest);
    }

    [Fact]
    public void WeekStart_IsAlwaysMonday()
    {
        Assert.Equal(D(2026, 7, 6), StreakCalculator.WeekStart(D(2026, 7, 6))); // Monday
        Assert.Equal(D(2026, 7, 6), StreakCalculator.WeekStart(D(2026, 7, 8))); // Wednesday
        Assert.Equal(D(2026, 7, 6), StreakCalculator.WeekStart(D(2026, 7, 12))); // Sunday
    }
}
