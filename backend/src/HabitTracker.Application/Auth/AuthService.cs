using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginWithExternalIdentityAsync(ExternalIdentity identity, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct = default);
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto> UpdateTimeZoneAsync(Guid userId, string timeZone, CancellationToken ct = default);
}

public class AuthService(
    IAppDbContext db,
    IPasswordHasherService passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    IClock clock,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<DeleteAccountRequest> deleteAccountValidator) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string SuspendedMessage = "This account has been suspended.";

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);
        var email = NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("This email address cannot be used.");
        }

        var user = new User
        {
            Email = email,
            TimeZone = request.TimeZone ?? "Europe/Istanbul"
        };
        user.PasswordHash = passwordHasher.Hash(request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);
        var email = NormalizeEmail(request.Email);
        var now = clock.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
        if (user is null)
        {
            // Equalize timing with a real verification to reduce enumeration signal.
            passwordHasher.Verify(passwordHasher.Hash("timing-equalizer-password"), request.Password);
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
        }

        // Google-only accounts have no password; password login must fail for them
        // with the same generic message, and still cost the same time.
        if (user.PasswordHash is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            await db.SaveChangesAsync(ct);
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        // Checked only after the password verified: a wrong password still returns
        // the same generic 401, so suspension is not an enumeration oracle.
        if (user.IsSuspended)
        {
            await db.SaveChangesAsync(ct);
            throw new ForbiddenAppException(SuspendedMessage);
        }

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>
    /// Signs in (or provisions) the account behind a verified external identity.
    /// Matching is by provider subject first, then by verified email so an existing
    /// password account gets linked instead of duplicated.
    /// </summary>
    public async Task<AuthResult> LoginWithExternalIdentityAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        if (!identity.EmailVerified)
        {
            // An unverified provider email could belong to someone else's account.
            throw new UnauthorizedAppException("The provider did not verify this email address.");
        }

        var email = NormalizeEmail(identity.Email);

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.GoogleSubject == identity.Subject && !u.IsDeleted, ct);

        if (user is null)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
            if (user is not null)
            {
                user.GoogleSubject = identity.Subject;
            }
            else
            {
                user = new User { Email = email, GoogleSubject = identity.Subject };
                db.Users.Add(user);
            }
        }
        else if (user.Email != email)
        {
            // The provider is authoritative for the address behind this subject.
            user.Email = email;
        }

        if (user.IsSuspended)
        {
            throw new ForbiddenAppException(SuspendedMessage);
        }

        // A successful external sign-in clears any password-brute-force lockout.
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedAppException("Missing refresh token.");
        }

        var hash = HashToken(refreshToken);
        var now = clock.UtcNow;
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.User is null || stored.User.IsDeleted || stored.User.IsSuspended)
        {
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        if (stored.RevokedAt is not null)
        {
            // Reuse of a rotated token indicates possible theft: revoke the whole family.
            var active = await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in active)
            {
                token.RevokedAt = now;
            }

            await db.SaveChangesAsync(ct);
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        var result = await IssueTokensAsync(stored.User, ct, saveImmediately: false);
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = HashToken(result.RefreshToken);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        await changePasswordValidator.ValidateAndThrowAsync(request, ct);
        var user = await GetActiveUserAsync(userId, ct);

        // A Google-only account has no password to prove; this call sets its first one.
        // The caller is already authenticated by a valid access token.
        if (user.PasswordHash is not null &&
            !passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            throw new UnauthorizedAppException("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await RevokeAllRefreshTokensAsync(userId, ct);
        return await IssueTokensAsync(user, ct);
    }

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct = default)
    {
        await deleteAccountValidator.ValidateAndThrowAsync(request, ct);
        var user = await GetActiveUserAsync(userId, ct);

        if (user.PasswordHash is not null &&
            !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedAppException("Password is incorrect.");
        }

        var now = clock.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        // Anonymize PII; keep the row so foreign keys and audit history stay intact.
        user.Email = $"deleted-{user.Id:N}@anonymized.invalid";
        user.PasswordHash = "!deleted";
        // Release the Google link so the same Google account can register again.
        user.GoogleSubject = null;
        // Sharing must not outlive the account, and the pairs are freed for reuse.
        user.ShareStreaksWithFriends = false;
        // A habit's name and description are text the user wrote about themselves, so
        // they are personal data and deletion has to reach them: an anonymized row with
        // the habits still hanging off it is not deleted data. Check-ins cascade from
        // the habit at the database level. This is the one deliberate exception to the
        // soft-delete rule in docs/SECURITY.md.
        var habits = await db.Habits.Where(h => h.UserId == userId).ToListAsync(ct);
        db.Habits.RemoveRange(habits);
        var friendships = await db.Friendships
            .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
            .ToListAsync(ct);
        db.Friendships.RemoveRange(friendships);
        // Group membership is consent too: it must not outlive the account.
        var memberships = await db.HabitGroupMembers.Where(m => m.UserId == userId).ToListAsync(ct);
        db.HabitGroupMembers.RemoveRange(memberships);
        var ownedGroups = await db.HabitGroups
            .Include(g => g.Members)
            .Where(g => g.OwnerId == userId)
            .ToListAsync(ct);
        foreach (var group in ownedGroups)
        {
            db.HabitGroupMembers.RemoveRange(group.Members);
        }

        db.HabitGroups.RemoveRange(ownedGroups);
        await RevokeAllRefreshTokensAsync(userId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetActiveUserAsync(userId, ct);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateTimeZoneAsync(Guid userId, string timeZone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 100 ||
            !Domain.Services.TimezoneHelper.IsValidTimeZone(timeZone))
        {
            throw new ValidationException("Unknown IANA timezone id.",
                [new FluentValidation.Results.ValidationFailure("timeZone", "Unknown IANA timezone id.")]);
        }

        var user = await GetActiveUserAsync(userId, ct);
        user.TimeZone = timeZone;
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
        {
            throw new UnauthorizedAppException("Account is no longer active.");
        }

        return user.IsSuspended ? throw new ForbiddenAppException(SuspendedMessage) : user;
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }
    }

    private async Task<AuthResult> IssueTokensAsync(User user, CancellationToken ct, bool saveImmediately = true)
    {
        var (accessToken, expiresIn) = tokenGenerator.GenerateAccessToken(user);
        var rawRefreshToken = GenerateRefreshToken();
        var refreshExpiry = clock.UtcNow.Add(RefreshTokenLifetime);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = refreshExpiry
        });

        if (saveImmediately)
        {
            await db.SaveChangesAsync(ct);
        }

        return new AuthResult(accessToken, expiresIn, rawRefreshToken, refreshExpiry, ToDto(user));
    }

    private static UserDto ToDto(User user) => UserMapper.ToDto(user);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
