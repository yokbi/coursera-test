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

    /// <summary>Opt-in: no reminder mail is ever sent unless the user turns this on.</summary>
    public bool RemindersEnabled { get; set; }
    /// <summary>Hour of the day (0-23) in the user's own timezone to send the reminder.</summary>
    public int ReminderHour { get; set; } = 20;
    /// <summary>Local day of the last reminder; makes repeated scheduler ticks idempotent.</summary>
    public DateOnly? LastReminderSentOn { get; set; }

    public ICollection<Habit> Habits { get; set; } = new List<Habit>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
