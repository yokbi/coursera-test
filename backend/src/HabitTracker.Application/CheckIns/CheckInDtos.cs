namespace HabitTracker.Application.CheckIns;

public sealed record CheckInDto(DateOnly Date, decimal Value, bool Completed);

/// <summary>When Increment is true, Value is added to the day's existing value; otherwise it replaces it.</summary>
public sealed record UpsertCheckInRequest(DateOnly Date, decimal Value, bool Increment = false);

public sealed record CheckInResultDto(CheckInDto CheckIn, int CurrentStreak, string StreakUnit);

public sealed record TodayHabitDto(
    Habits.HabitDto Habit,
    decimal TodayValue,
    bool CompletedToday,
    int CurrentStreak,
    string StreakUnit,
    // For TimesPerWeek habits: completions in the current ISO week.
    int WeekCompletions);

public sealed record WeekDayCellDto(DateOnly Date, decimal Value, bool Completed, bool Scheduled);

public sealed record WeekHabitRowDto(Habits.HabitDto Habit, IReadOnlyList<WeekDayCellDto> Days);

public sealed record WeekOverviewDto(DateOnly WeekStart, IReadOnlyList<WeekHabitRowDto> Habits);
