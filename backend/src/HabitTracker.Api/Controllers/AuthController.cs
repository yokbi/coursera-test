using HabitTracker.Api.Infrastructure;
using HabitTracker.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HabitTracker.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    public const string RefreshCookieName = "ht_refresh";
    private const string RefreshCookiePath = "/api/v1/auth";
    private const string CsrfHeaderName = "X-CSRF";

    public sealed record AuthResponse(string AccessToken, int ExpiresInSeconds, UserDto User);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        SetRefreshCookie(result);
        return Ok(ToResponse(result));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        SetRefreshCookie(result);
        return Ok(ToResponse(result));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        RequireCsrfHeader();
        var refreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
        var result = await authService.RefreshAsync(refreshToken, ct);
        SetRefreshCookie(result);
        return Ok(ToResponse(result));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        RequireCsrfHeader();
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await authService.ChangePasswordAsync(User.GetUserId(), request, ct);
        SetRefreshCookie(result);
        return Ok(ToResponse(result));
    }

    [Authorize]
    [HttpDelete("account")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken ct)
    {
        await authService.DeleteAccountAsync(User.GetUserId(), request, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    private static AuthResponse ToResponse(AuthResult result) =>
        new(result.AccessToken, result.ExpiresInSeconds, result.User);

    /// <summary>
    /// Cross-site requests cannot set custom headers, so requiring one on
    /// cookie-authenticated endpoints blocks CSRF even if SameSite were bypassed.
    /// </summary>
    private void RequireCsrfHeader()
    {
        if (!Request.Headers.ContainsKey(CsrfHeaderName))
        {
            throw new Application.Common.Exceptions.UnauthorizedAppException("Missing CSRF header.");
        }
    }

    private void SetRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(result.RefreshTokenExpiresAt, DateTimeKind.Utc))
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath
        });
    }
}
