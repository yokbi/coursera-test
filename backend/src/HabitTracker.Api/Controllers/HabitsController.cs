using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.CheckIns;
using HabitTracker.Application.Habits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/habits")]
public class HabitsController(
    IHabitService habitService,
    ICheckInService checkInService,
    IDashboardService dashboardService,
    IHabitStatsService statsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<HabitDto>>> List(
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await habitService.ListAsync(User.GetUserId(), includeArchived, page, pageSize, ct));

    [HttpPost]
    public async Task<ActionResult<HabitDto>> Create(CreateHabitRequest request, CancellationToken ct)
    {
        var habit = await habitService.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { habitId = habit.Id }, habit);
    }

    [HttpGet("today")]
    public async Task<ActionResult<IReadOnlyList<TodayHabitDto>>> Today(CancellationToken ct) =>
        Ok(await dashboardService.GetTodayAsync(User.GetUserId(), ct));

    [HttpGet("week")]
    public async Task<ActionResult<WeekOverviewDto>> Week([FromQuery] DateOnly? weekStart, CancellationToken ct) =>
        Ok(await dashboardService.GetWeekAsync(User.GetUserId(), weekStart, ct));

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(ReorderHabitsRequest request, CancellationToken ct)
    {
        await habitService.ReorderAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpGet("{habitId:guid}")]
    public async Task<ActionResult<HabitDto>> Get(Guid habitId, CancellationToken ct) =>
        Ok(await habitService.GetAsync(User.GetUserId(), habitId, ct));

    [HttpPut("{habitId:guid}")]
    public async Task<ActionResult<HabitDto>> Update(Guid habitId, UpdateHabitRequest request, CancellationToken ct) =>
        Ok(await habitService.UpdateAsync(User.GetUserId(), habitId, request, ct));

    [HttpPost("{habitId:guid}/archive")]
    public async Task<ActionResult<HabitDto>> Archive(Guid habitId, CancellationToken ct) =>
        Ok(await habitService.ArchiveAsync(User.GetUserId(), habitId, archived: true, ct));

    [HttpPost("{habitId:guid}/unarchive")]
    public async Task<ActionResult<HabitDto>> Unarchive(Guid habitId, CancellationToken ct) =>
        Ok(await habitService.ArchiveAsync(User.GetUserId(), habitId, archived: false, ct));

    [HttpPut("{habitId:guid}/checkins")]
    public async Task<ActionResult<CheckInResultDto>> UpsertCheckIn(
        Guid habitId, UpsertCheckInRequest request, CancellationToken ct) =>
        Ok(await checkInService.UpsertAsync(User.GetUserId(), habitId, request, ct));

    [HttpGet("{habitId:guid}/checkins")]
    public async Task<ActionResult<IReadOnlyList<CheckInDto>>> ListCheckIns(
        Guid habitId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await checkInService.ListAsync(User.GetUserId(), habitId, from, to, ct));

    [HttpGet("{habitId:guid}/stats")]
    public async Task<ActionResult<HabitStatsDto>> Stats(Guid habitId, CancellationToken ct) =>
        Ok(await statsService.GetStatsAsync(User.GetUserId(), habitId, ct));
}
