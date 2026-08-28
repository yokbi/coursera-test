namespace HabitTracker.Application.Groups;

public sealed record CreateGroupRequest(
    string Name, string? Description, string Color, string Icon, Guid HabitId);

public sealed record InviteMemberRequest(Guid UserId);

public sealed record JoinGroupRequest(Guid HabitId);

/// <summary>Progress of one joined member, scoped to the habit they linked here.</summary>
public sealed record GroupMemberProgressDto(
    Guid UserId,
    string Email,
    bool IsOwner,
    bool IsYou,
    string HabitName,
    string HabitIcon,
    int CurrentStreak,
    string StreakUnit,
    bool CompletedToday,
    bool ScheduledToday,
    int CompletionsLast7Days,
    DateTime JoinedAt);

public sealed record GroupInviteeDto(Guid UserId, string Email);

/// <summary>
/// Summary shown in the list. Member counts are safe for an invitee to see;
/// per-member progress is not, and lives only on the detail endpoint.
/// </summary>
public sealed record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    string Icon,
    bool IsOwner,
    bool Joined,
    int MemberCount,
    DateTime CreatedAt);

public sealed record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    string Icon,
    bool IsOwner,
    IReadOnlyList<GroupMemberProgressDto> Members,
    IReadOnlyList<GroupInviteeDto> PendingInvitees,
    DateTime CreatedAt);
