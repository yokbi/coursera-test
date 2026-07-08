using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HabitTracker.Infrastructure.Services;

/// <summary>ASP.NET Identity default hashing: PBKDF2-HMAC-SHA256, 100k iterations, 128-bit salt.</summary>
public class PasswordHasherService : IPasswordHasherService
{
    private static readonly User Dummy = new();
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string hash, string password)
    {
        try
        {
            var result = _hasher.VerifyHashedPassword(Dummy, hash, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // Anonymized/invalid hash sentinel values never verify.
            return false;
        }
    }
}
