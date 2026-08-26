using HabitTracker.Domain.Common;
using HabitTracker.Domain.Enums;

namespace HabitTracker.Domain.Entities;

/// <summary>
/// One row per relationship, keyed by who asked. A pair is stored once: the
/// reverse direction is found by matching either column, never by a second row.
/// </summary>
public class Friendship : BaseEntity
{
    public Guid RequesterId { get; set; }
    public User? Requester { get; set; }

    public Guid AddresseeId { get; set; }
    public User? Addressee { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime? RespondedAt { get; set; }

    /// <summary>The other party's id, from the point of view of the given user.</summary>
    public Guid OtherPartyId(Guid userId) => userId == RequesterId ? AddresseeId : RequesterId;

    public bool Involves(Guid userId) => userId == RequesterId || userId == AddresseeId;
}
