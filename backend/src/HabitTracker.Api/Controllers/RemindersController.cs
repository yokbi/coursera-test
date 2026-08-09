using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Auth;
using HabitTracker.Application.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/v1/reminders")]
public class RemindersController(IReminderService reminderService) : ControllerBase
{
    [Authorize]
    [HttpPut("settings")]
    public async Task<ActionResult<UserDto>> UpdateSettings(ReminderSettingsRequest request, CancellationToken ct) =>
        Ok(await reminderService.UpdateSettingsAsync(User.GetUserId(), request, ct));

    /// <summary>
    /// Reached from a link in the mail itself, so it cannot require a session.
    /// The signed token only ever disables reminders — it grants no other access.
    /// </summary>
    [HttpGet("unsubscribe")]
    [Produces("text/html")]
    public async Task<ContentResult> Unsubscribe([FromQuery] string? token, CancellationToken ct)
    {
        var ok = await reminderService.UnsubscribeAsync(token ?? string.Empty, ct);
        var message = ok
            ? "Hatırlatma e-postaları kapatıldı."
            : "Bu bağlantı geçersiz veya süresi dolmuş.";

        return new ContentResult
        {
            StatusCode = ok ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest,
            ContentType = "text/html; charset=utf-8",
            Content = $"""
                       <!doctype html>
                       <html lang="tr"><head><meta charset="utf-8">
                       <title>Alışkanlık Takibi</title></head>
                       <body style="font-family:sans-serif;padding:2rem">
                       <p>{message}</p>
                       </body></html>
                       """
        };
    }
}
