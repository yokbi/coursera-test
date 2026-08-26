using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Application.Habits;
using HabitTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Admin;

public interface IAdminService
{
    Task<PagedResult<AdminUserDto>> ListUsersAsync(
        string? search, bool includeDeleted, int page, int pageSize, CancellationToken ct = default);

    Task<AdminUserDto> SetSuspendedAsync(Guid actingAdminId, Guid userId, bool suspended, CancellationToken ct = default);

    Task<AdminMetricsDto> GetMetricsAsync(CancellationToken ct = default);
}

public class AdminService(IAppDbContext db, IClock clock) : IAdminService
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(
        string? search, bool includeDeleted, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Users.AsNoTracking();
        if (!includeDeleted)
        {
            query = query.Where(u => !u.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Parameterized by EF Core; never string-concatenated into SQL.
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.Role,
                u.TimeZone,
                u.SuspendedAt != null,
                u.SuspendedAt,
                u.IsDeleted,
                u.RemindersEnabled,
                u.Habits.Count(h => !h.IsArchived),
                u.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminUserDto>(users, page, pageSize, total);
    }

    public async Task<AdminUserDto> SetSuspendedAsync(
        Guid actingAdminId, Guid userId, bool suspended, CancellationToken ct = default)
    {
        // Locking yourself out of the panel is never the intent.
        if (actingAdminId == userId && suspended)
        {
            throw new DomainRuleException("An admin cannot suspend their own account.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                   ?? throw new NotFoundException("User not found.");

        if (suspended && user.Role == UserRole.Admin)
        {
            throw new DomainRuleException("Admin accounts cannot be suspended.");
        }

        user.SuspendedAt = suspended ? clock.UtcNow : null;
        if (suspended)
        {
            // Cut existing sessions immediately rather than waiting for expiry.
            var tokens = await db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.RevokedAt = clock.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);

        var habitCount = await db.Habits.CountAsync(h => h.UserId == userId && !h.IsArchived, ct);
        return new AdminUserDto(
            user.Id, user.Email, user.Role, user.TimeZone,
            user.SuspendedAt != null, user.SuspendedAt, user.IsDeleted,
            user.RemindersEnabled, habitCount, user.CreatedAt);
    }

    public async Task<AdminMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var weekAgo = today.AddDays(-6);
        var thirtyDaysAgo = clock.UtcNow.AddDays(-30);

        return new AdminMetricsDto(
            TotalUsers: await db.Users.CountAsync(ct),
            ActiveUsers: await db.Users.CountAsync(u => !u.IsDeleted && u.SuspendedAt == null, ct),
            SuspendedUsers: await db.Users.CountAsync(u => !u.IsDeleted && u.SuspendedAt != null, ct),
            DeletedUsers: await db.Users.CountAsync(u => u.IsDeleted, ct),
            UsersWithRemindersOn: await db.Users.CountAsync(u => !u.IsDeleted && u.RemindersEnabled, ct),
            TotalHabits: await db.Habits.CountAsync(h => !h.IsArchived, ct),
            ArchivedHabits: await db.Habits.CountAsync(h => h.IsArchived, ct),
            CheckInsLast7Days: await db.CheckIns.CountAsync(c => c.Date >= weekAgo && c.Date <= today, ct),
            NewUsersLast30Days: await db.Users.CountAsync(u => u.CreatedAt >= thirtyDaysAgo, ct));
    }
}
