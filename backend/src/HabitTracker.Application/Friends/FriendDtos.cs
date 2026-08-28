namespace HabitTracker.Application.Friends;

public sealed record SendFriendRequest(string Email);

public sealed record SharingSettingsRequest(bool ShareStreaks);

/// <summary>
/// What a friend is allowed to see. Every stat is null unless that friend has
/// explicitly turned sharing on — absence of consent means absence of data.
/// </summary>
public sealed record FriendDto(
    Guid UserId,
    string Email,
    bool SharingEnabled,
    int? ActiveHabits,
    int? BestStreak,
    string? BestStreakUnit,
    string? BestStreakHabitName,
    int? CompletedToday,
    int? ScheduledToday,
    DateTime FriendsSince);

public sealed record FriendRequestDto(
    Guid RequestId,
    Guid UserId,
    string Email,
    DateTime CreatedAt);

public sealed record FriendRequestsDto(
    IReadOnlyList<FriendRequestDto> Incoming,
    IReadOnlyList<FriendRequestDto> Outgoing);
