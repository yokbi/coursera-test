using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/groups")]
public class GroupsController(IGroupService groupService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupSummaryDto>>> List(CancellationToken ct) =>
        Ok(await groupService.ListAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<ActionResult<GroupSummaryDto>> Create(CreateGroupRequest request, CancellationToken ct)
    {
        var group = await groupService.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { groupId = group.Id }, group);
    }

    [HttpGet("{groupId:guid}")]
    public async Task<ActionResult<GroupDetailDto>> Get(Guid groupId, CancellationToken ct) =>
        Ok(await groupService.GetAsync(User.GetUserId(), groupId, ct));

    [HttpPost("{groupId:guid}/invitations")]
    public async Task<IActionResult> Invite(Guid groupId, InviteMemberRequest request, CancellationToken ct)
    {
        await groupService.InviteAsync(User.GetUserId(), groupId, request, ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/join")]
    public async Task<IActionResult> Join(Guid groupId, JoinGroupRequest request, CancellationToken ct)
    {
        await groupService.JoinAsync(User.GetUserId(), groupId, request, ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid groupId, CancellationToken ct)
    {
        await groupService.DeclineAsync(User.GetUserId(), groupId, ct);
        return NoContent();
    }

    [HttpPut("{groupId:guid}/habit")]
    public async Task<IActionResult> ChangeHabit(Guid groupId, JoinGroupRequest request, CancellationToken ct)
    {
        await groupService.ChangeHabitAsync(User.GetUserId(), groupId, request, ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid groupId, CancellationToken ct)
    {
        await groupService.LeaveAsync(User.GetUserId(), groupId, ct);
        return NoContent();
    }

    [HttpDelete("{groupId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        await groupService.RemoveMemberAsync(User.GetUserId(), groupId, memberUserId, ct);
        return NoContent();
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Delete(Guid groupId, CancellationToken ct)
    {
        await groupService.DeleteAsync(User.GetUserId(), groupId, ct);
        return NoContent();
    }
}
