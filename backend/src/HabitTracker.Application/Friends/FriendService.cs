using HabitTracker.Application.CheckIns;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Enums;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Friends;

public interface IFriendService
{
    Task SendRequestAsync(Guid userId, SendFriendRequest request, CancellationToken ct = default);
    Task<FriendRequestsDto> GetRequestsAsync(Guid userId, CancellationToken ct = default);
    Task AcceptAsync(Guid userId, Guid requestId, CancellationToken ct = default);
    Task DeclineAsync(Guid userId, Guid requestId, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, Guid friendUserId, CancellationToken ct = default);
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default);
    Task<Auth.UserDto> UpdateSharingAsync(Guid userId, SharingSettingsRequest request, CancellationToken ct = default);
}

public class FriendService(IAppDbContext db, IClock clock) : IFriendService
{
    /// <summary>
    /// Always succeeds, whether or not the address belongs to an account. Reporting
    /// "no such user" would turn this endpoint into an account-enumeration oracle,
    /// which the rest of the API is careful to avoid.
    /// </summary>
    public async Task SendRequestAsync(Guid userId, SendFriendRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (email.Length is 0 or > 320)
        {
            throw new FluentValidation.ValidationException("A valid email address is required.",
                [new FluentValidation.Results.ValidationFailure("email", "A valid email address is required.")]);
        }

        var target = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted && u.SuspendedAt == null, ct);

        // Unknown address, or your own: silently a no-op.
        if (target is null || target.Id == userId)
        {
            return;
        }

        var existing = await FindBetweenAsync(userId, target.Id, ct);
        if (existing is not null)
        {
            switch (existing.Status)
            {
                case FriendshipStatus.Accepted:
                    return; // already friends

                case FriendshipStatus.Pending when existing.AddresseeId == userId:
                    // They asked first: asking back is consent, so link them now.
                    existing.Status = FriendshipStatus.Accepted;
                    existing.RespondedAt = clock.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;

                case FriendshipStatus.Pending:
                    return; // already waiting on them

                case FriendshipStatus.Declined:
                    // A refusal is not permanent; reopen the same row.
                    existing.RequesterId = userId;
                    existing.AddresseeId = target.Id;
                    existing.Status = FriendshipStatus.Pending;
                    existing.RespondedAt = null;
                    await db.SaveChangesAsync(ct);
                    return;
            }
        }

        db.Friendships.Add(new Friendship { RequesterId = userId, AddresseeId = target.Id });
        await db.SaveChangesAsync(ct);
    }

    public async Task<FriendRequestsDto> GetRequestsAsync(Guid userId, CancellationToken ct = default)
    {
        var pending = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var incoming = pending
            .Where(f => f.AddresseeId == userId)
            .Select(f => new FriendRequestDto(f.Id, f.RequesterId, f.Requester!.Email, f.CreatedAt))
            .ToList();

        var outgoing = pending
            .Where(f => f.RequesterId == userId)
            .Select(f => new FriendRequestDto(f.Id, f.AddresseeId, f.Addressee!.Email, f.CreatedAt))
            .ToList();

        return new FriendRequestsDto(incoming, outgoing);
    }

    public async Task AcceptAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        // Only the addressee can accept: the requester accepting their own ask
        // would let anyone friend anyone.
        var request = await db.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId
                                      && f.AddresseeId == userId
                                      && f.Status == FriendshipStatus.Pending, ct)
                      ?? throw new NotFoundException("Friend request not found.");

        request.Status = FriendshipStatus.Accepted;
        request.RespondedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeclineAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var request = await db.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId
                                      && f.AddresseeId == userId
                                      && f.Status == FriendshipStatus.Pending, ct)
                      ?? throw new NotFoundException("Friend request not found.");

        request.Status = FriendshipStatus.Declined;
        request.RespondedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid userId, Guid friendUserId, CancellationToken ct = default)
    {
        var friendship = await FindBetweenAsync(userId, friendUserId, ct);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            throw new NotFoundException("Friendship not found.");
        }

        // Deleted outright so the pair can start over cleanly, and so neither side
        // keeps any visibility for even a moment.
        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default)
    {
        var friendships = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync(ct);

        var result = new List<FriendDto>();
        foreach (var friendship in friendships)
        {
            var friend = friendship.RequesterId == userId ? friendship.Addressee! : friendship.Requester!;
            if (friend.IsDeleted)
            {
                continue;
            }

            var since = friendship.RespondedAt ?? friendship.CreatedAt;

            if (!friend.ShareStreaksWithFriends)
            {
                // Consent withheld: identity only, no habit data of any kind.
                result.Add(new FriendDto(friend.Id, friend.Email, false,
                    null, null, null, null, null, null, since));
                continue;
            }

            result.Add(await BuildSharedSummaryAsync(friend, since, ct));
        }

        return result.OrderByDescending(f => f.BestStreak ?? -1).ThenBy(f => f.Email).ToList();
    }

    private async Task<FriendDto> BuildSharedSummaryAsync(User friend, DateTime since, CancellationToken ct)
    {
        var localToday = TimezoneHelper.IsValidTimeZone(friend.TimeZone)
            ? TimezoneHelper.LocalDate(clock.UtcNow, friend.TimeZone)
            : DateOnly.FromDateTime(clock.UtcNow);

        var habits = await db.Habits.AsNoTracking()
            .Where(h => h.UserId == friend.Id && !h.IsArchived)
            .ToListAsync(ct);

        var bestStreak = 0;
        string? bestUnit = null;
        string? bestName = null;
        var completedToday = 0;
        var scheduledToday = 0;

        foreach (var habit in habits)
        {
            if (habit.IsScheduledOn(localToday))
            {
                scheduledToday++;
                var threshold = habit.CompletionThreshold;
                var todayValue = await db.CheckIns.AsNoTracking()
                    .Where(c => c.HabitId == habit.Id && c.Date == localToday)
                    .Select(c => (decimal?)c.Value)
                    .FirstOrDefaultAsync(ct) ?? 0;
                if (todayValue > 0 && todayValue >= threshold)
                {
                    completedToday++;
                }
            }

            var streak = await CheckInService.ComputeStreakAsync(db, habit, localToday, ct);
            if (streak.Current > bestStreak)
            {
                bestStreak = streak.Current;
                bestUnit = streak.Unit;
                bestName = habit.Name;
            }
        }

        return new FriendDto(
            friend.Id, friend.Email, true,
            habits.Count, bestStreak, bestUnit ?? "days", bestName,
            completedToday, scheduledToday, since);
    }

    public async Task<Auth.UserDto> UpdateSharingAsync(Guid userId, SharingSettingsRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedAppException("Account is no longer active.");

        user.ShareStreaksWithFriends = request.ShareStreaks;
        await db.SaveChangesAsync(ct);
        return Auth.UserMapper.ToDto(user);
    }

    /// <summary>Finds the single row for a pair, in whichever direction it was created.</summary>
    private Task<Friendship?> FindBetweenAsync(Guid a, Guid b, CancellationToken ct) =>
        db.Friendships.FirstOrDefaultAsync(
            f => (f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a), ct);
}
