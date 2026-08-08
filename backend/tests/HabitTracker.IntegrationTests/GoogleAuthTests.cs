using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HabitTracker.Application.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

/// <summary>
/// Stands in for Google so the whole redirect/callback flow runs without real
/// credentials. The authorization URL points back at the callback so the test
/// client can follow it like a browser would.
/// </summary>
public class FakeExternalAuthClient : IExternalAuthClient
{
    public ExternalIdentity NextIdentity { get; set; } = new("google-sub-1", "user@example.com", true);
    public string? LastCodeVerifier { get; private set; }
    public string? LastCodeChallenge { get; private set; }

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        LastCodeChallenge = codeChallenge;
        return $"/api/v1/auth/google/callback?code=test-code&state={Uri.EscapeDataString(state)}";
    }

    public Task<ExternalIdentity> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        LastCodeVerifier = codeVerifier;
        return Task.FromResult(NextIdentity);
    }
}

[Collection("api")]
public class GoogleAuthTests(PostgresFixture postgres) : IDisposable
{
    private readonly FakeExternalAuthClient _google = new();
    private readonly TestWebAppFactory _base = new(postgres);
    private WebApplicationFactory<Program>? _configured;

    public void Dispose()
    {
        _configured?.Dispose();
        _base.Dispose();
    }

    private HttpClient CreateClient(bool followRedirects = false)
    {
        _configured ??= _base.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IExternalAuthClient>();
                services.AddSingleton<IExternalAuthClient>(_google);
            });
        });

        return _configured.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = followRedirects
        });
    }

    /// <summary>Walks start → (fake Google) → callback, carrying cookies like a browser.</summary>
    private async Task<HttpResponseMessage> SignInWithGoogleAsync(HttpClient client, string? returnPath = null)
    {
        var startUrl = "/api/v1/auth/google/start" +
                       (returnPath is null ? "" : $"?returnPath={Uri.EscapeDataString(returnPath)}");
        var start = await client.GetAsync(startUrl);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);

        var flowCookie = ExtractCookie(start, "ht_oauth");
        var callback = new HttpRequestMessage(HttpMethod.Get, start.Headers.Location!.ToString());
        callback.Headers.Add("Cookie", flowCookie);
        return await client.SendAsync(callback);
    }

    private static string ExtractCookie(HttpResponseMessage response, string name)
    {
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{name}="))
            : null;
        return setCookie?.Split(';')[0] ?? string.Empty;
    }

    [Fact]
    public async Task Available_ReportsWhetherCredentialsAreConfigured()
    {
        var unconfigured = await _base.CreateClient().GetAsync("/api/v1/auth/google/available");
        var unconfiguredBody = await unconfigured.Content.ReadAsStringAsync();
        Assert.Contains("false", unconfiguredBody);

        var configured = await CreateClient().GetAsync("/api/v1/auth/google/available");
        Assert.Contains("true", await configured.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Start_SetsLaxFlowCookieAndRedirects()
    {
        var start = await CreateClient().GetAsync("/api/v1/auth/google/start");

        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var setCookie = start.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("ht_oauth="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        // Strict would not survive Google's cross-site redirect back to the callback.
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        // PKCE challenge must be sent, and must not be the raw verifier.
        Assert.False(string.IsNullOrEmpty(_google.LastCodeChallenge));
    }

    [Fact]
    public async Task Callback_CreatesAccountAndIssuesRefreshCookie()
    {
        var client = CreateClient();
        _google.NextIdentity = new("google-new-user", UniqueEmail(), true);

        var callback = await SignInWithGoogleAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("http://localhost:3000/giris/google?next=%2F", callback.Headers.Location!.ToString());

        var refreshCookie = ExtractCookie(callback, "ht_refresh");
        Assert.StartsWith("ht_refresh=", refreshCookie);

        // The issued session works: refresh returns an access token for the new user.
        var refresh = await client.SendAsync(RefreshRequest(refreshCookie));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var (auth, _) = await ReadAuthAsync(refresh);
        Assert.Equal(_google.NextIdentity.Email, auth.User.Email);
    }

    [Fact]
    public async Task Callback_SendsPkceVerifierMatchingTheChallenge()
    {
        var client = CreateClient();
        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", UniqueEmail(), true);

        await SignInWithGoogleAsync(client);

        Assert.False(string.IsNullOrEmpty(_google.LastCodeVerifier));
        var expected = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.ASCII.GetBytes(_google.LastCodeVerifier!)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(expected, _google.LastCodeChallenge);
    }

    [Fact]
    public async Task Callback_SecondSignIn_ReusesTheSameAccount()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", email, true);

        var first = await SignInWithGoogleAsync(client);
        var firstAuth = await client.SendAsync(RefreshRequest(ExtractCookie(first, "ht_refresh")));
        var (firstResult, _) = await ReadAuthAsync(firstAuth);

        var second = await SignInWithGoogleAsync(client);
        var secondAuth = await client.SendAsync(RefreshRequest(ExtractCookie(second, "ht_refresh")));
        var (secondResult, _) = await ReadAuthAsync(secondAuth);

        Assert.Equal(firstResult.User.Id, secondResult.User.Id);
    }

    [Fact]
    public async Task Callback_LinksToExistingPasswordAccountWithSameEmail()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (registered, _) = await client.RegisterAsync(email);

        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", email, true);
        var callback = await SignInWithGoogleAsync(client);
        var refresh = await client.SendAsync(RefreshRequest(ExtractCookie(callback, "ht_refresh")));
        var (linked, _) = await ReadAuthAsync(refresh);

        // Same account, now carrying both sign-in methods.
        Assert.Equal(registered.User.Id, linked.User.Id);
        Assert.True(linked.User.HasPassword);
        Assert.True(linked.User.LinkedGoogle);

        // The original password still works.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Callback_UnverifiedProviderEmail_IsRejected()
    {
        var client = CreateClient();
        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", UniqueEmail(), EmailVerified: false);

        var callback = await SignInWithGoogleAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode);
    }

    [Fact]
    public async Task Callback_WithoutFlowCookie_IsRejected()
    {
        var client = CreateClient();
        var start = await client.GetAsync("/api/v1/auth/google/start");

        // Same callback URL, but the browser "lost" the state cookie.
        var callback = await client.GetAsync(start.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode);
    }

    [Fact]
    public async Task Callback_WithMismatchedState_IsRejected()
    {
        var client = CreateClient();
        var start = await client.GetAsync("/api/v1/auth/google/start");
        var flowCookie = ExtractCookie(start, "ht_oauth");

        var forged = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/auth/google/callback?code=test-code&state=attacker-state");
        forged.Headers.Add("Cookie", flowCookie);
        var callback = await client.SendAsync(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode);
    }

    [Fact]
    public async Task Callback_ConsentDenied_RedirectsBackToLogin()
    {
        var client = CreateClient();
        var start = await client.GetAsync("/api/v1/auth/google/start");
        var flowCookie = ExtractCookie(start, "ht_oauth");

        var denied = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/auth/google/callback?error=access_denied");
        denied.Headers.Add("Cookie", flowCookie);
        var callback = await client.SendAsync(denied);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("http://localhost:3000/giris?hata=google", callback.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("https://evil.example/steal", "http://localhost:3000/giris/google?next=%2F")]
    [InlineData("//evil.example", "http://localhost:3000/giris/google?next=%2F")]
    [InlineData("/aliskanliklar", "http://localhost:3000/giris/google?next=%2Faliskanliklar")]
    public async Task Callback_ReturnPath_CannotBecomeAnOpenRedirect(string requested, string expected)
    {
        var client = CreateClient();
        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", UniqueEmail(), true);

        var callback = await SignInWithGoogleAsync(client, requested);

        Assert.Equal(expected, callback.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GoogleOnlyAccount_CannotLogInWithAPassword_ButCanSetOne()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        _google.NextIdentity = new($"sub-{Guid.NewGuid():N}", email, true);

        var callback = await SignInWithGoogleAsync(client);
        var refresh = await client.SendAsync(RefreshRequest(ExtractCookie(callback, "ht_refresh")));
        var (auth, _) = await ReadAuthAsync(refresh);
        Assert.False(auth.User.HasPassword);

        // No password set yet, so password login is refused with the generic message.
        var attempt = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Guessed1x!" });
        Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);

        // The authenticated user can set a first password without proving an old one.
        var setPassword = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "", newPassword = "N3wStr0ngPass!" })
        }.WithBearer(auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(setPassword)).StatusCode);

        // And can then log in with it.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "N3wStr0ngPass!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task PasswordAccount_StillRequiresItsCurrentPasswordToChange()
    {
        var client = CreateClient();
        var (auth, _) = await client.RegisterAsync();

        var attempt = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "", newPassword = "N3wStr0ngPass!" })
        }.WithBearer(auth.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(attempt)).StatusCode);
    }

    [Fact]
    public async Task DeletedGoogleAccount_CanSignUpAgainWithTheSameGoogleIdentity()
    {
        var client = CreateClient();
        var subject = $"sub-{Guid.NewGuid():N}";
        _google.NextIdentity = new(subject, UniqueEmail(), true);

        var first = await SignInWithGoogleAsync(client);
        var refresh = await client.SendAsync(RefreshRequest(ExtractCookie(first, "ht_refresh")));
        var (auth, _) = await ReadAuthAsync(refresh);

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password = "" })
        }.WithBearer(auth.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        // The Google link was released, so the same identity provisions a fresh account.
        _google.NextIdentity = new(subject, UniqueEmail(), true);
        var second = await SignInWithGoogleAsync(client);
        var secondRefresh = await client.SendAsync(RefreshRequest(ExtractCookie(second, "ht_refresh")));
        var (secondAuth, _) = await ReadAuthAsync(secondRefresh);

        Assert.NotEqual(auth.User.Id, secondAuth.User.Id);
    }
}
