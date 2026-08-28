using FluentValidation;
using HabitTracker.Application.CheckIns;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Enums;
using HabitTracker.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Application.Groups;

public interface IGroupService
{
    Task<GroupSummaryDto> CreateAsync(Guid userId, CreateGroupRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GroupSummaryDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<GroupDetailDto> GetAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task InviteAsync(Guid userId, Guid groupId, InviteMemberRequest request, CancellationToken ct = default);
    Task JoinAsync(Guid userId, Guid groupId, JoinGroupRequest request, CancellationToken ct = default);
    Task DeclineAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task LeaveAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid userId, Guid groupId, Guid memberUserId, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task ChangeHabitAsync(Guid userId, Guid groupId, JoinGroupRequest request, CancellationToken ct = default);
}

public class GroupService(
    IAppDbContext db,
    IClock clock,
    IValidator<CreateGroupRequest> createValidator,
    IValidator<JoinGroupRequest> joinValidator) : IGroupService
{
    public async Task<GroupSummaryDto> CreateAsync(Guid userId, CreateGroupRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureOwnedHabitAsync(userId, request.HabitId, ct);

        var group = new HabitGroup
        {
            OwnerId = userId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Color = request.Color,
            Icon = request.Icon
        };
        db.HabitGroups.Add(group);

        // The creator joins immediately: creating a group is itself consent.
        db.HabitGroupMembers.Add(new HabitGroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            HabitId = request.HabitId,
            Status = GroupMemberStatus.Joined,
            JoinedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return new GroupSummaryDto(group.Id, group.Name, group.Description, group.Color, group.Icon,
            IsOwner: true, Joined: true, MemberCount: 1, group.CreatedAt);
    }

    public async Task<IReadOnlyList<GroupSummaryDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var memberships = await db.HabitGroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Include(m => m.Group)
            .ToListAsync(ct);

        var groupIds = memberships.Select(m => m.GroupId).ToList();
        var counts = await db.HabitGroupMembers.AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Joined)
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, ct);

        return memberships
            .Where(m => m.Group is not null)
            .Select(m => new GroupSummaryDto(
                m.Group!.Id, m.Group.Name, m.Group.Description, m.Group.Color, m.Group.Icon,
                IsOwner: m.Group.OwnerId == userId,
                Joined: m.Status == GroupMemberStatus.Joined,
                MemberCount: counts.GetValueOrDefault(m.GroupId),
                m.Group.CreatedAt))
            .OrderByDescending(g => g.CreatedAt)
            .ToList();
    }

    public async Task<GroupDetailDto> GetAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);

        // Only a *joined* member may see anyone's progress. An outstanding invitation
        // is not consent, so it grants no view of the other members.
        var self = group.Members.FirstOrDefault(m => m.UserId == userId)
                   ?? throw new NotFoundException("Group not found.");
        if (self.Status != GroupMemberStatus.Joined)
        {
            throw new ForbiddenAppException("Join the group to see its members.");
        }

        var members = new List<GroupMemberProgressDto>();
        foreach (var member in group.Members.Where(m => m.Status == GroupMemberStatus.Joined))
        {
            if (member.Habit is null || member.User is null || member.User.IsDeleted)
            {
                continue;
            }

            members.Add(await BuildProgressAsync(group, member, userId, ct));
        }

        var pending = group.OwnerId == userId
            ? group.Members
                .Where(m => m.Status == GroupMemberStatus.Invited && m.User is not null)
                .Select(m => new GroupInviteeDto(m.UserId, m.User!.Email))
                .ToList()
            : [];

        return new GroupDetailDto(
            group.Id, group.Name, group.Description, group.Color, group.Icon,
            group.OwnerId == userId,
            members.OrderByDescending(m => m.CurrentStreak).ThenBy(m => m.Email).ToList(),
            pending,
            group.CreatedAt);
    }

    private async Task<GroupMemberProgressDto> BuildProgressAsync(
        HabitGroup group, HabitGroupMember member, Guid viewerId, CancellationToken ct)
    {
        var habit = member.Habit!;
        var owner = member.User!;
        var localToday = TimezoneHelper.IsValidTimeZone(owner.TimeZone)
            ? TimezoneHelper.LocalDate(clock.UtcNow, owner.TimeZone)
            : DateOnly.FromDateTime(clock.UtcNow);

        var threshold = habit.CompletionThreshold;
        var todayValue = await db.CheckIns.AsNoTracking()
            .Where(c => c.HabitId == habit.Id && c.Date == localToday)
            .Select(c => (decimal?)c.Value)
            .FirstOrDefaultAsync(ct) ?? 0;

        var weekAgo = localToday.AddDays(-6);
        var last7 = await db.CheckIns.AsNoTracking()
            .CountAsync(c => c.HabitId == habit.Id
                             && c.Date >= weekAgo && c.Date <= localToday
                             && c.Value >= threshold && c.Value > 0, ct);

        var streak = await CheckInService.ComputeStreakAsync(db, habit, localToday, ct);

        return new GroupMemberProgressDto(
            owner.Id, owner.Email,
            IsOwner: group.OwnerId == owner.Id,
            IsYou: owner.Id == viewerId,
            habit.Name, habit.Icon,
            streak.Current, streak.Unit,
            CompletedToday: todayValue > 0 && todayValue >= threshold,
            ScheduledToday: habit.IsScheduledOn(localToday),
            CompletionsLast7Days: last7,
            member.JoinedAt ?? member.CreatedAt);
    }

    public async Task InviteAsync(Guid userId, Guid groupId, InviteMemberRequest request, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        if (group.OwnerId != userId)
        {
            throw new NotFoundException("Group not found.");
        }

        if (request.UserId == userId)
        {
            throw new DomainRuleException("You are already in this group.");
        }

        // Only accepted friends can be invited: a group must not become a way to
        // push yourself into a stranger's app.
        var areFriends = await db.Friendships.AnyAsync(
            f => f.Status == FriendshipStatus.Accepted &&
                 ((f.RequesterId == userId && f.AddresseeId == request.UserId) ||
                  (f.RequesterId == request.UserId && f.AddresseeId == userId)), ct);
        if (!areFriends)
        {
            throw new DomainRuleException("You can only invite your friends.");
        }

        if (group.Members.Any(m => m.UserId == request.UserId))
        {
            return; // already invited or joined
        }

        db.HabitGroupMembers.Add(new HabitGroupMember
        {
            GroupId = groupId,
            UserId = request.UserId,
            Status = GroupMemberStatus.Invited
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task JoinAsync(Guid userId, Guid groupId, JoinGroupRequest request, CancellationToken ct = default)
    {
        await joinValidator.ValidateAndThrowAsync(request, ct);
        await EnsureOwnedHabitAsync(userId, request.HabitId, ct);

        var member = await db.HabitGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId
                                      && m.UserId == userId
                                      && m.Status == GroupMemberStatus.Invited, ct)
                     ?? throw new NotFoundException("Invitation not found.");

        member.HabitId = request.HabitId;
        member.Status = GroupMemberStatus.Joined;
        member.JoinedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeclineAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var member = await db.HabitGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId
                                      && m.UserId == userId
                                      && m.Status == GroupMemberStatus.Invited, ct)
                     ?? throw new NotFoundException("Invitation not found.");

        db.HabitGroupMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        var member = group.Members.FirstOrDefault(m => m.UserId == userId)
                     ?? throw new NotFoundException("Group not found.");

        if (group.OwnerId == userId)
        {
            // The owner leaving would strand the group; deleting it is the honest action.
            throw new DomainRuleException("The owner cannot leave; delete the group instead.");
        }

        await RemoveMemberRowAsync(member, ct);
    }

    public async Task RemoveMemberAsync(Guid userId, Guid groupId, Guid memberUserId, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        if (group.OwnerId != userId)
        {
            throw new NotFoundException("Group not found.");
        }

        if (memberUserId == userId)
        {
            throw new DomainRuleException("The owner cannot leave; delete the group instead.");
        }

        var member = group.Members.FirstOrDefault(m => m.UserId == memberUserId)
                     ?? throw new NotFoundException("Member not found.");

        await RemoveMemberRowAsync(member, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await LoadGroupAsync(groupId, ct);
        if (group.OwnerId != userId)
        {
            throw new NotFoundException("Group not found.");
        }

        db.HabitGroupMembers.RemoveRange(group.Members);
        db.HabitGroups.Remove(group);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeHabitAsync(Guid userId, Guid groupId, JoinGroupRequest request, CancellationToken ct = default)
    {
        await joinValidator.ValidateAndThrowAsync(request, ct);
        await EnsureOwnedHabitAsync(userId, request.HabitId, ct);

        var member = await db.HabitGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId
                                      && m.UserId == userId
                                      && m.Status == GroupMemberStatus.Joined, ct)
                     ?? throw new NotFoundException("Group not found.");

        member.HabitId = request.HabitId;
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveMemberRowAsync(HabitGroupMember member, CancellationToken ct)
    {
        var tracked = await db.HabitGroupMembers.FirstOrDefaultAsync(m => m.Id == member.Id, ct);
        if (tracked is not null)
        {
            db.HabitGroupMembers.Remove(tracked);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<HabitGroup> LoadGroupAsync(Guid groupId, CancellationToken ct) =>
        await db.HabitGroups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Members).ThenInclude(m => m.Habit)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct)
        ?? throw new NotFoundException("Group not found.");

    /// <summary>
    /// A member may only ever link a habit they own. Without this a caller could
    /// point a membership at someone else's habit and read its progress back.
    /// </summary>
    private async Task EnsureOwnedHabitAsync(Guid userId, Guid habitId, CancellationToken ct)
    {
        var owned = await db.Habits.AnyAsync(h => h.Id == habitId && h.UserId == userId && !h.IsArchived, ct);
        if (!owned)
        {
            throw new NotFoundException("Habit not found.");
        }
    }
}
