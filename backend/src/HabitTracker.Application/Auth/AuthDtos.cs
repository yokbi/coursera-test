namespace HabitTracker.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string? TimeZone);

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record DeleteAccountRequest(string Password);

/// <summary>
/// HasPassword lets the UI offer "set a password" instead of "change password"
/// for accounts created through Google; LinkedGoogle drives the linked-account badge.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string TimeZone,
    DateTime CreatedAt,
    bool HasPassword,
    bool LinkedGoogle,
    bool RemindersEnabled,
    int ReminderHour,
    bool IsAdmin,
    bool ShareStreaksWithFriends);

/// <summary>RefreshToken is the raw opaque token; the API layer moves it into an httpOnly cookie.</summary>
public sealed record AuthResult(string AccessToken, int ExpiresInSeconds, string RefreshToken, DateTime RefreshTokenExpiresAt, UserDto User);
