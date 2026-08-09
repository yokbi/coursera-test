using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Auth;

/// <summary>Single place that decides what of a User is safe to expose.</summary>
public static class UserMapper
{
    public static UserDto ToDto(User user) => new(
        user.Id,
        user.Email,
        user.TimeZone,
        user.CreatedAt,
        user.PasswordHash is not null,
        user.GoogleSubject is not null,
        user.RemindersEnabled,
        user.ReminderHour);
}
