using HabitTracker.Domain.Common;

namespace HabitTracker.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    /// <summary>Null for accounts created through an external provider that never set a password.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Google's stable `sub` claim; set once the account is linked to Google.</summary>
    public string? GoogleSubject { get; set; }
    /// <summary>IANA timezone id used to compute the user's local day boundaries.</summary>
    public string TimeZone { get; set; } = "Europe/Istanbul";
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public ICollection<Habit> Habits { get; set; } = new List<Habit>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
