using HabitTracker.Domain.Enums;

namespace HabitTracker.Application.Admin;

/// <summary>
/// Admin-facing view of an account. Deliberately excludes password hashes,
/// provider subjects and refresh tokens — an admin manages accounts, not secrets.
/// </summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    UserRole Role,
    string TimeZone,
    bool IsSuspended,
    DateTime? SuspendedAt,
    bool IsDeleted,
    bool RemindersEnabled,
    int HabitCount,
    DateTime CreatedAt);

public sealed record AdminMetricsDto(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int DeletedUsers,
    int UsersWithRemindersOn,
    int TotalHabits,
    int ArchivedHabits,
    int CheckInsLast7Days,
    int NewUsersLast30Days);
