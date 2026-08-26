using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Admin;
using HabitTracker.Application.Habits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

/// <summary>
/// Admin-only surface. Role comes from the JWT, so every action here is gated
/// without an extra lookup; ordinary habit endpoints are untouched and still
/// scope strictly to the caller — admins get no window into anyone's habits.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> ListUsers(
        [FromQuery] string? search,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await adminService.ListUsersAsync(search, includeDeleted, page, pageSize, ct));

    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<ActionResult<AdminUserDto>> Suspend(Guid userId, CancellationToken ct) =>
        Ok(await adminService.SetSuspendedAsync(User.GetUserId(), userId, suspended: true, ct));

    [HttpPost("users/{userId:guid}/unsuspend")]
    public async Task<ActionResult<AdminUserDto>> Unsuspend(Guid userId, CancellationToken ct) =>
        Ok(await adminService.SetSuspendedAsync(User.GetUserId(), userId, suspended: false, ct));

    [HttpGet("metrics")]
    public async Task<ActionResult<AdminMetricsDto>> Metrics(CancellationToken ct) =>
        Ok(await adminService.GetMetricsAsync(ct));
}
