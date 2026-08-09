using HabitTracker.Domain.Enums;
using HabitTracker.Domain.Services;

namespace HabitTracker.UnitTests.Domain;

public class ReminderPlannerTests
{
    private static DateTime Local(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0);

    // --- ShouldSend ---

    [Fact]
    public void DoesNotSend_WhenRemindersAreOff()
    {
        Assert.False(ReminderPlanner.ShouldSend(
            remindersEnabled: false, reminderHour: 20, lastSentOn: null,
            localNow: Local(2026, 7, 9, 21), pendingHabitCount: 3));
    }

    [Fact]
    public void DoesNotSend_WhenNothingIsPending()
    {
        Assert.False(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20, lastSentOn: null,
            localNow: Local(2026, 7, 9, 21), pendingHabitCount: 0));
    }

    [Fact]
    public void DoesNotSend_BeforeThePreferredHour()
    {
        Assert.False(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20, lastSentOn: null,
            localNow: Local(2026, 7, 9, 19), pendingHabitCount: 2));
    }

    [Fact]
    public void Sends_AtTheExactPreferredHour()
    {
        Assert.True(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20, lastSentOn: null,
            localNow: Local(2026, 7, 9, 20), pendingHabitCount: 1));
    }

    [Fact]
    public void Sends_LateSameDay_AfterASchedulerOutage()
    {
        // Missed the 20:00 tick; 23:00 the same local day still delivers.
        Assert.True(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20, lastSentOn: null,
            localNow: Local(2026, 7, 9, 23), pendingHabitCount: 1));
    }

    [Fact]
    public void DoesNotSendTwice_OnTheSameLocalDay()
    {
        Assert.False(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20,
            lastSentOn: new DateOnly(2026, 7, 9),
            localNow: Local(2026, 7, 9, 22), pendingHabitCount: 4));
    }

    [Fact]
    public void SendsAgain_TheNextLocalDay()
    {
        Assert.True(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 20,
            lastSentOn: new DateOnly(2026, 7, 9),
            localNow: Local(2026, 7, 10, 20), pendingHabitCount: 1));
    }

    [Fact]
    public void MidnightPreference_SendsFromTheStartOfTheDay()
    {
        Assert.True(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: 0, lastSentOn: null,
            localNow: Local(2026, 7, 9, 0), pendingHabitCount: 1));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(99)]
    public void OutOfRangeHour_IsClampedRatherThanCrashing(int hour)
    {
        // Clamped to 0 or 23; either way a 23:00 local time is at or past it.
        Assert.True(ReminderPlanner.ShouldSend(
            remindersEnabled: true, reminderHour: hour, lastSentOn: null,
            localNow: Local(2026, 7, 9, 23), pendingHabitCount: 1));
    }

    // --- IsPending ---

    [Fact]
    public void DailyHabit_IsPending_UntilTheTargetIsReached()
    {
        Assert.True(Pending(ScheduleType.Daily, threshold: 8, todayValue: 3));
        Assert.False(Pending(ScheduleType.Daily, threshold: 8, todayValue: 8));
        Assert.False(Pending(ScheduleType.Daily, threshold: 8, todayValue: 10));
    }

    [Fact]
    public void WeekdayHabit_IsNeverPending_OnAnUnscheduledDay()
    {
        // 2026-07-09 is a Thursday; the habit only runs Mon/Wed/Fri.
        Assert.False(ReminderPlanner.IsPending(
            ScheduleType.SpecificWeekdays,
            Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday,
            null, 1m, 0m, 0, new DateOnly(2026, 7, 9)));
    }

    [Fact]
    public void WeekdayHabit_IsPending_OnAScheduledDay()
    {
        // 2026-07-10 is a Friday.
        Assert.True(ReminderPlanner.IsPending(
            ScheduleType.SpecificWeekdays,
            Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday,
            null, 1m, 0m, 0, new DateOnly(2026, 7, 10)));
    }

    [Fact]
    public void WeeklyQuotaHabit_IsJudgedByTheWeek_NotTheDay()
    {
        // Target 2/week: still short at 1, satisfied at 2, even with nothing today.
        Assert.True(ReminderPlanner.IsPending(
            ScheduleType.TimesPerWeek, Weekdays.None, 2, 1m, 0m, 1, new DateOnly(2026, 7, 9)));
        Assert.False(ReminderPlanner.IsPending(
            ScheduleType.TimesPerWeek, Weekdays.None, 2, 1m, 0m, 2, new DateOnly(2026, 7, 9)));
    }

    private static bool Pending(ScheduleType type, decimal threshold, decimal todayValue) =>
        ReminderPlanner.IsPending(type, Weekdays.None, null, threshold, todayValue, 0, new DateOnly(2026, 7, 9));
}
