using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HabitTracker.Application.Auth;
using HabitTracker.Application.Common.Exceptions;
using HabitTracker.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HabitTracker.Api.Controllers;

/// <summary>
/// Server-side authorization-code flow. The browser never sees the client secret,
/// and the resulting session is the same rotating refresh cookie the password flow
/// issues, so the rest of the API is unchanged.
/// </summary>
[ApiController]
[Route("api/v1/auth/google")]
public class GoogleAuthController(
    IExternalAuthClient externalAuthClient,
    IAuthService authService,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<GoogleOAuthOptions> googleOptions) : ControllerBase
{
    private const string FlowCookieName = "ht_oauth";
    private readonly FrontendOptions _frontend = frontendOptions.Value;

    private sealed record FlowState(string State, string CodeVerifier, string ReturnPath);

    /// <summary>Lets the UI hide the Google button on servers without credentials.</summary>
    [HttpGet("available")]
    public ActionResult<GoogleAvailabilityDto> Available() =>
        Ok(new GoogleAvailabilityDto(googleOptions.Value.IsConfigured));

    public sealed record GoogleAvailabilityDto(bool Available);

    [HttpGet("start")]
    [EnableRateLimiting("auth")]
    public IActionResult Start([FromQuery] string? returnPath)
    {
        var state = RandomToken();
        var codeVerifier = RandomToken();
        var flow = new FlowState(state, codeVerifier, SafeReturnPath(returnPath));

        // SameSite=Lax (not Strict): the cookie must survive Google's top-level
        // redirect back to the callback, which is a cross-site navigation.
        Response.Cookies.Append(FlowCookieName, Protect(flow), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth/google",
            Expires = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        return Redirect(externalAuthClient.BuildAuthorizationUrl(state, Challenge(codeVerifier)));
    }

    [HttpGet("callback")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        var flow = ReadFlowCookie();
        ClearFlowCookie();

        if (!string.IsNullOrEmpty(error))
        {
            // User declined consent — send them back to login rather than erroring out.
            return Redirect($"{_frontend.BaseUrl}/giris?hata=google");
        }

        if (flow is null || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            throw new UnauthorizedAppException("Invalid Google sign-in callback.");
        }

        // Constant-time comparison so a mismatched state cannot be probed byte by byte.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(flow.State), Encoding.UTF8.GetBytes(state)))
        {
            throw new UnauthorizedAppException("Invalid Google sign-in callback.");
        }

        var identity = await externalAuthClient.ExchangeCodeAsync(code, flow.CodeVerifier, ct);
        var result = await authService.LoginWithExternalIdentityAsync(identity, ct);

        Response.Cookies.Append(AuthController.RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(result.RefreshTokenExpiresAt, DateTimeKind.Utc))
        });

        // Land on a public bootstrap page rather than the target directly: the route
        // guard keys off a marker cookie that only the frontend can set, and that page
        // sets it by calling /auth/refresh before forwarding to the requested path.
        return Redirect($"{_frontend.BaseUrl}{_frontend.OAuthLandingPath}" +
                        $"?next={Uri.EscapeDataString(flow.ReturnPath)}");
    }

    private FlowState? ReadFlowCookie()
    {
        var raw = Request.Cookies[FlowCookieName];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FlowState>(
                Encoding.UTF8.GetString(Base64UrlDecode(raw)));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    private void ClearFlowCookie() => Response.Cookies.Delete(FlowCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/api/v1/auth/google"
    });

    private static string Protect(FlowState flow) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(flow)));

    /// <summary>Only same-origin relative paths, so the callback cannot become an open redirect.</summary>
    private static string SafeReturnPath(string? returnPath) =>
        !string.IsNullOrEmpty(returnPath) &&
        returnPath.StartsWith('/') &&
        !returnPath.StartsWith("//", StringComparison.Ordinal)
            ? returnPath
            : "/";

    private static string RandomToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string codeVerifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Origin the OAuth callback redirects back to; no trailing slash.</summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Public frontend page that exchanges the refresh cookie for a session.</summary>
    public string OAuthLandingPath { get; set; } = "/giris/google";
}
