namespace HabitTracker.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string? TimeZone);

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record DeleteAccountRequest(string Password);

public sealed record UserDto(Guid Id, string Email, string TimeZone, DateTime CreatedAt);

/// <summary>RefreshToken is the raw opaque token; the API layer moves it into an httpOnly cookie.</summary>
public sealed record AuthResult(string AccessToken, int ExpiresInSeconds, string RefreshToken, DateTime RefreshTokenExpiresAt, UserDto User);
