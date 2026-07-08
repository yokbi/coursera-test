using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Enums;

namespace HabitTracker.Application.Habits;

public sealed record HabitDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    string Icon,
    string? Category,
    HabitType Type,
    decimal? TargetValue,
    string? Unit,
    ScheduleType ScheduleType,
    IReadOnlyList<string> ScheduleDays,
    int? TimesPerWeek,
    int SortOrder,
    bool IsArchived,
    DateOnly StartDate,
    DateTime CreatedAt);

public sealed record CreateHabitRequest(
    string Name,
    string? Description,
    string Color,
    string Icon,
    string? Category,
    HabitType Type,
    decimal? TargetValue,
    string? Unit,
    ScheduleType ScheduleType,
    IReadOnlyList<string>? ScheduleDays,
    int? TimesPerWeek);

public sealed record UpdateHabitRequest(
    string Name,
    string? Description,
    string Color,
    string Icon,
    string? Category,
    decimal? TargetValue,
    string? Unit,
    ScheduleType ScheduleType,
    IReadOnlyList<string>? ScheduleDays,
    int? TimesPerWeek);

public sealed record ReorderHabitsRequest(IReadOnlyList<Guid> HabitIds);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class WeekdaysMapper
{
    private static readonly (string Name, Weekdays Flag)[] Map =
    [
        ("monday", Weekdays.Monday),
        ("tuesday", Weekdays.Tuesday),
        ("wednesday", Weekdays.Wednesday),
        ("thursday", Weekdays.Thursday),
        ("friday", Weekdays.Friday),
        ("saturday", Weekdays.Saturday),
        ("sunday", Weekdays.Sunday)
    ];

    public static IReadOnlyList<string> ToStrings(Weekdays days) =>
        Map.Where(m => days.HasFlag(m.Flag)).Select(m => m.Name).ToList();

    public static Weekdays FromStrings(IEnumerable<string>? names) =>
        names?.Aggregate(Weekdays.None, (acc, name) =>
            acc | (Map.FirstOrDefault(m => m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).Flag))
        ?? Weekdays.None;

    public static bool AreValidNames(IEnumerable<string>? names) =>
        names is null || names.All(n => Map.Any(m => m.Name.Equals(n.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static HabitDto ToDto(this Habit habit) => new(
        habit.Id, habit.Name, habit.Description, habit.Color, habit.Icon, habit.Category,
        habit.Type, habit.TargetValue, habit.Unit, habit.ScheduleType,
        ToStrings(habit.ScheduleDays), habit.TimesPerWeek, habit.SortOrder,
        habit.IsArchived, habit.StartDate, habit.CreatedAt);
}
