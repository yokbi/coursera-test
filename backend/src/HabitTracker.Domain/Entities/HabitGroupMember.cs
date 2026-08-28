using HabitTracker.Domain.Common;
using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Entities;

/// <summary>
/// Membership and invitation in one row: an invitation is a member whose status is
/// still Invited. Joining is the consent that makes the linked habit's progress
/// visible to the group — independent of the friend-list sharing flag.
/// </summary>
public class HabitGroupMember : BaseEntity
{
    public Guid GroupId { get; set; }
    public HabitGroup? Group { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>The member's own habit tracked for this group; set when they join.</summary>
    public Guid? HabitId { get; set; }
    public Habit? Habit { get; set; }

    public GroupMemberStatus Status { get; set; } = GroupMemberStatus.Invited;
    public DateTime? JoinedAt { get; set; }
}
