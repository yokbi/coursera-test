using HabitTracker.Domain.Common;

namespace HabitTracker.Domain.Entities;

public class CheckIn : BaseEntity
{
    public Guid HabitId { get; set; }
    public Habit? Habit { get; set; }
    /// <summary>Denormalized owner id for the hot user_id + date index.</summary>
    public Guid UserId { get; set; }

    /// <summary>Calendar day in the habit owner's timezone.</summary>
    public DateOnly Date { get; set; }
    /// <summary>1/0 for Boolean habits; amount for Quantity; minutes for Duration.</summary>
    public decimal Value { get; set; }
}
