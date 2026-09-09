using System.Net;
using System.Net.Http.Json;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

[Collection("api")]
public class AuthFlowTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestWebAppFactory _factory = new(postgres);

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Register_ReturnsAccessTokenAndRefreshCookie()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password = "Str0ngPass!x" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (auth, refreshCookie) = await ReadAuthAsync(response);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.True(auth.ExpiresInSeconds is > 0 and <= 900);
        Assert.StartsWith("ht_refresh=", refreshCookie);

        var setCookie = response.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("ht_refresh="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("short1A")]     // too short
    [InlineData("alllowercase1")] // no uppercase
    [InlineData("ALLUPPERCASE1")] // no lowercase
    [InlineData("NoDigitsHere!")] // no digit
    public async Task Register_WeakPassword_Returns400WithProblemDetails(string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = UniqueEmail(), password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors", body);
        Assert.Contains("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "not-an-email", password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnSameGenericError()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPass1x" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = UniqueEmail(), password = "WrongPass1x" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Identical bodies prevent user enumeration.
        Assert.Equal(await unknownEmail.Content.ReadAsStringAsync(),
            await wrongPassword.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_Success_ReturnsTokens()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (auth, cookie) = await ReadAuthAsync(response);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.StartsWith("ht_refresh=", cookie);
    }

    [Fact]
    public async Task Login_AfterFiveFailures_LocksOutEvenWithCorrectPassword()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "WrongPass1x" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var lockedOut = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.Unauthorized, lockedOut.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: "America/New_York");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me").WithBearer(auth.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserInfoDto>(Json);
        Assert.Equal(email, profile!.Email);
        Assert.Equal("America/New_York", profile.TimeZone);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeZone_InvalidId_Returns400()
    {
        var client = CreateClient();
        var (auth, _) = await client.RegisterAsync();

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/me")
        {
            Content = JsonContent.Create(new { timeZone = "Mars/OlympusMons" })
        }.WithBearer(auth.AccessToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenReuseRevokesFamily()
    {
        var client = CreateClient();
        var (_, originalCookie) = await client.RegisterAsync();

        // First refresh succeeds and rotates.
        var first = await client.SendAsync(RefreshRequest(originalCookie));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var (_, rotatedCookie) = await ReadAuthAsync(first);
        Assert.NotEqual(originalCookie, rotatedCookie);

        // Reusing the original (now revoked) token fails...
        var reuse = await client.SendAsync(RefreshRequest(originalCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ...and revokes the whole family: the rotated token is now dead too.
        var afterReuse = await client.SendAsync(RefreshRequest(rotatedCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCsrfHeader_Returns401()
    {
        var client = CreateClient();
        var (_, cookie) = await client.RegisterAsync();

        var response = await client.SendAsync(RefreshRequest(cookie, includeCsrf: false));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("X-CSRF", "1");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = CreateClient();
        var (_, cookie) = await client.RegisterAsync();

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("Cookie", cookie);
        logout.Headers.Add("X-CSRF", "1");
        var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refresh = await client.SendAsync(RefreshRequest(cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOldSessionsAndOldPassword()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (auth, oldCookie) = await client.RegisterAsync(email);

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "Str0ngPass!x", newPassword = "N3wStr0ngPass!" })
        }.WithBearer(auth.AccessToken);
        var changeResponse = await client.SendAsync(change);
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        // Old refresh token no longer works.
        var oldRefresh = await client.SendAsync(RefreshRequest(oldCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);

        // Old password fails, new password works.
        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "N3wStr0ngPass!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns401()
    {
        var client = CreateClient();
        var (auth, _) = await client.RegisterAsync();

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "WrongPass1x", newPassword = "N3wStr0ngPass!" })
        }.WithBearer(auth.AccessToken);
        var response = await client.SendAsync(change);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_SoftDeletesAndBlocksLoginAndTokens()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (auth, cookie) = await client.RegisterAsync(email);

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password = "Str0ngPass!x" })
        }.WithBearer(auth.AccessToken);
        var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Login is refused with the generic message.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // The still-unexpired access token can no longer access the profile.
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me").WithBearer(auth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(me)).StatusCode);

        // Refresh tokens are revoked.
        var refresh = await client.SendAsync(RefreshRequest(cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // The email can be reused after anonymization.
        var reRegister = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.OK, reRegister.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_TakesTheHabitsAndCheckInsWithIt()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        // A habit's name is text the user wrote about themselves, and a check-in says
        // what they did on a given day. Both are personal data, so deletion has to
        // reach them - an anonymized user row with the habits still hanging off it
        // would not be deleted data.
        var created = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Sabah yuruyusu",
            color = "#22c55e",
            icon = "\U0001F6B6",
            type = "boolean",
            scheduleType = "daily"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
            Assert.True(await db.Habits.AnyAsync(h => h.UserId == userId));
        }

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password = "Str0ngPass!x" })
        }.WithBearer(auth.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Habits.AnyAsync(h => h.UserId == userId));
            // Check-ins cascade from the habit at the database level.
            Assert.False(await db.CheckIns.AnyAsync(c => c.UserId == userId));
            // The row itself stays, anonymized, so foreign keys and audit history hold.
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.True(user.IsDeleted);
            Assert.DoesNotContain(email, user.Email);
        }
    }

    [Fact]
    public async Task ProblemDetails_DoesNotLeakStackTraces()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = UniqueEmail(), password = "WrongPass1x" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at HabitTracker", body);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
    }
}
