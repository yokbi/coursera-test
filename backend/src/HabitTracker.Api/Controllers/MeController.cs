using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController(IAuthService authService) : ControllerBase
{
    public sealed record UpdateProfileRequest(string TimeZone);

    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct) =>
        Ok(await authService.GetProfileAsync(User.GetUserId(), ct));

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await authService.UpdateTimeZoneAsync(User.GetUserId(), request.TimeZone, ct));
}
