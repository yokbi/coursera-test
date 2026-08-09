using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HabitTracker.IntegrationTests;

public sealed record AuthResponseDto(string AccessToken, int ExpiresInSeconds, UserInfoDto User);

public sealed record UserInfoDto(
    Guid Id, string Email, string TimeZone, DateTime CreatedAt,
    bool HasPassword, bool LinkedGoogle, bool RemindersEnabled, int ReminderHour);

public static class ApiClientHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.local";

    public static async Task<(AuthResponseDto Auth, string RefreshCookie)> RegisterAsync(
        this HttpClient client, string? email = null, string password = "Str0ngPass!x", string? timeZone = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = email ?? UniqueEmail(), password, timeZone });
        response.EnsureSuccessStatusCode();
        return await ReadAuthAsync(response);
    }

    public static async Task<(AuthResponseDto Auth, string RefreshCookie)> ReadAuthAsync(HttpResponseMessage response)
    {
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(Json)
                   ?? throw new InvalidOperationException("Empty auth response.");
        return (auth, ExtractRefreshCookie(response));
    }

    /// <summary>Raw `ht_refresh=...` pair from Set-Cookie, usable as a Cookie request header value.</summary>
    public static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("ht_refresh="))
            : null;
        return setCookie?.Split(';')[0] ?? string.Empty;
    }

    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public static HttpRequestMessage RefreshRequest(string refreshCookie, bool includeCsrf = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", refreshCookie);
        if (includeCsrf)
        {
            request.Headers.Add("X-CSRF", "1");
        }

        return request;
    }
}
