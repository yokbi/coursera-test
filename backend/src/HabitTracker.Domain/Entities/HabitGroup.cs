using HabitTracker.Domain.Common;

namespace HabitTracker.Domain.Entities;

/// <summary>
/// A shared goal several friends pursue together. Nobody's habit is shared: each
/// member links one of their *own* habits, and only that habit's group-scoped
/// progress becomes visible to the other joined members.
/// </summary>
public class HabitGroup : BaseEntity
{
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#22c55e";
    public string Icon { get; set; } = "👥";

    public ICollection<HabitGroupMember> Members { get; set; } = new List<HabitGroupMember>();
}
