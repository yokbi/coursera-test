using HabitTracker.Domain.Common;
using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Entities;

public class Habit : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Hex color like #22c55e.</summary>
    public string Color { get; set; } = "#22c55e";
    /// <summary>Emoji icon.</summary>
    public string Icon { get; set; } = "✅";
    public string? Category { get; set; }

    public HabitType Type { get; set; }
    /// <summary>Target amount for Quantity habits or minutes for Duration habits; null for Boolean.</summary>
    public decimal? TargetValue { get; set; }
    /// <summary>Unit label for Quantity habits (e.g. "bardak").</summary>
    public string? Unit { get; set; }

    public ScheduleType ScheduleType { get; set; }
    /// <summary>Scheduled weekdays bitmask; only meaningful for SpecificWeekdays.</summary>
    public Weekdays ScheduleDays { get; set; }
    /// <summary>Weekly quota; only meaningful for TimesPerWeek.</summary>
    public int? TimesPerWeek { get; set; }

    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    /// <summary>The habit's first calendar day in the user's timezone; streaks never look further back.</summary>
    public DateOnly StartDate { get; set; }

    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();

    /// <summary>A day's check-in counts as completed when its value reaches the target (1 for Boolean).</summary>
    public decimal CompletionThreshold => Type == HabitType.Boolean ? 1m : TargetValue ?? 1m;

    public bool IsScheduledOn(DateOnly date) => ScheduleType switch
    {
        ScheduleType.Daily => true,
        ScheduleType.SpecificWeekdays => ScheduleDays.Includes(date),
        // TimesPerWeek habits can be done any day; quota is evaluated per ISO week.
        _ => true
    };
}
