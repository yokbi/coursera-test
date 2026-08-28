using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Auth;
using HabitTracker.Application.Friends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/friends")]
public class FriendsController(IFriendService friendService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendDto>>> List(CancellationToken ct) =>
        Ok(await friendService.GetFriendsAsync(User.GetUserId(), ct));

    /// <summary>
    /// Always 204, whether or not the address belongs to an account — otherwise the
    /// response would reveal which emails are registered.
    /// </summary>
    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest(SendFriendRequest request, CancellationToken ct)
    {
        await friendService.SendRequestAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpGet("requests")]
    public async Task<ActionResult<FriendRequestsDto>> Requests(CancellationToken ct) =>
        Ok(await friendService.GetRequestsAsync(User.GetUserId(), ct));

    [HttpPost("requests/{requestId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid requestId, CancellationToken ct)
    {
        await friendService.AcceptAsync(User.GetUserId(), requestId, ct);
        return NoContent();
    }

    [HttpPost("requests/{requestId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid requestId, CancellationToken ct)
    {
        await friendService.DeclineAsync(User.GetUserId(), requestId, ct);
        return NoContent();
    }

    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> Remove(Guid friendUserId, CancellationToken ct)
    {
        await friendService.RemoveAsync(User.GetUserId(), friendUserId, ct);
        return NoContent();
    }

    [HttpPut("sharing")]
    public async Task<ActionResult<UserDto>> UpdateSharing(SharingSettingsRequest request, CancellationToken ct) =>
        Ok(await friendService.UpdateSharingAsync(User.GetUserId(), request, ct));
}
