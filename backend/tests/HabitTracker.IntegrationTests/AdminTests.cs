using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HabitTracker.Domain.Enums;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

[Collection("api")]
public class AdminTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestWebAppFactory _factory = new(postgres);

    public void Dispose() => _factory.Dispose();

    /// <summary>Registers an account, then promotes it in the database and signs in again
    /// so the issued token actually carries the Admin role claim.</summary>
    private async Task<(HttpClient Client, string Email, Guid Id)> CreateAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.RegisterAsync(email);

        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
            id = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        var (auth, _) = await ReadAuthAsync(login);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return (client, email, id);
    }

    private async Task<(HttpClient Client, string Email, Guid Id)> CreateUserAsync()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return (client, email, auth.User.Id);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    // --- Authorization boundary ---

    [Fact]
    public async Task AdminEndpoints_RejectAnonymousCallers()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/metrics")).StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_RejectOrdinaryUsersWith403()
    {
        var (client, _, _) = await CreateUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/suspend", null)).StatusCode);
    }

    [Fact]
    public async Task PromotedUser_MustSignInAgainBeforeTheRoleTakesEffect()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        // The old token still carries role=User; the claim is only refreshed on a new token.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        var (fresh, _) = await ReadAuthAsync(login);
        client.DefaultRequestHeaders.Authorization = new("Bearer", fresh.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_GetsNoWindowIntoAnotherUsersHabits()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var (userClient, _, _) = await CreateUserAsync();

        var created = await userClient.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Gizli", color = "#000000", icon = "🔒", type = "boolean", scheduleType = "daily"
        });
        var habitId = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        // Being an admin does not widen the ordinary, owner-scoped endpoints.
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"/api/v1/habits/{habitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"/api/v1/habits/{habitId}/stats")).StatusCode);
    }

    // --- User listing ---

    [Fact]
    public async Task ListUsers_FindsByEmailAndPaginates()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var (_, targetEmail, _) = await CreateUserAsync();

        var found = await ReadJsonAsync(await adminClient.GetAsync($"/api/v1/admin/users?search={targetEmail}"));
        Assert.Equal(1, found.GetProperty("totalCount").GetInt32());
        Assert.Equal(targetEmail, found.GetProperty("items")[0].GetProperty("email").GetString());

        var paged = await ReadJsonAsync(await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=2"));
        Assert.True(paged.GetProperty("items").GetArrayLength() <= 2);
        Assert.Equal(2, paged.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListUsers_NeverExposesSecrets()
    {
        var (adminClient, _, _) = await CreateAdminAsync();

        var body = await (await adminClient.GetAsync("/api/v1/admin/users")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleSubject", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListUsers_ReportsActiveHabitCount()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var (userClient, targetEmail, _) = await CreateUserAsync();

        foreach (var name in new[] { "Bir", "İki" })
        {
            await userClient.PostAsJsonAsync("/api/v1/habits", new
            {
                name, color = "#22c55e", icon = "✅", type = "boolean", scheduleType = "daily"
            });
        }

        var listed = await ReadJsonAsync(await adminClient.GetAsync($"/api/v1/admin/users?search={targetEmail}"));
        Assert.Equal(2, listed.GetProperty("items")[0].GetProperty("habitCount").GetInt32());
    }

    // --- Suspension ---

    [Fact]
    public async Task Suspend_BlocksLoginAndKillsExistingSessions()
    {
        var (adminClient, _, _) = await CreateAdminAsync();

        var victim = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, refreshCookie) = await victim.RegisterAsync(email);
        victim.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var userId = auth.User.Id;

        var suspend = await adminClient.PostAsync($"/api/v1/admin/users/{userId}/suspend", null);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.True((await ReadJsonAsync(suspend)).GetProperty("isSuspended").GetBoolean());

        // Correct password, but the account is suspended: 403, not the generic 401.
        var login = await victim.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        // Existing refresh tokens were revoked, so the live session cannot be renewed.
        Assert.Equal(HttpStatusCode.Unauthorized, (await victim.SendAsync(RefreshRequest(refreshCookie))).StatusCode);

        // And the still-unexpired access token no longer reaches the profile.
        Assert.Equal(HttpStatusCode.Forbidden, (await victim.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Suspend_WithAWrongPassword_StillReturnsTheGeneric401()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var victim = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await victim.RegisterAsync(email);

        await adminClient.PostAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", null);

        // Suspension must not become an oracle: a bad password looks the same as ever.
        var wrong = await victim.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPass1x" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Unsuspend_RestoresAccess()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var victim = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await victim.RegisterAsync(email);

        await adminClient.PostAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", null);
        var unsuspend = await adminClient.PostAsync($"/api/v1/admin/users/{auth.User.Id}/unsuspend", null);
        Assert.Equal(HttpStatusCode.OK, unsuspend.StatusCode);
        Assert.False((await ReadJsonAsync(unsuspend)).GetProperty("isSuspended").GetBoolean());

        var login = await victim.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Str0ngPass!x" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotSuspendThemselvesOrAnotherAdmin()
    {
        var (adminClient, _, adminId) = await CreateAdminAsync();
        var (_, _, otherAdminId) = await CreateAdminAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await adminClient.PostAsync($"/api/v1/admin/users/{adminId}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await adminClient.PostAsync($"/api/v1/admin/users/{otherAdminId}/suspend", null)).StatusCode);
    }

    [Fact]
    public async Task Suspend_UnknownUser_Returns404()
    {
        var (adminClient, _, _) = await CreateAdminAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await adminClient.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/suspend", null)).StatusCode);
    }

    [Fact]
    public async Task SuspendedUser_IsNotMailedByTheReminderSweep()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var victim = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await victim.RegisterAsync(email, timeZone: "UTC");
        victim.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        await victim.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Meditasyon", color = "#8b5cf6", icon = "🧘", type = "boolean", scheduleType = "daily"
        });
        await victim.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 0 });

        await adminClient.PostAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
        Assert.True(stored.SuspendedAt is not null);
    }

    // --- Metrics ---

    [Fact]
    public async Task Metrics_CountAccountsAndHabits()
    {
        var (adminClient, _, _) = await CreateAdminAsync();
        var (userClient, _, _) = await CreateUserAsync();
        await userClient.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Ölçüm", color = "#0ea5e9", icon = "📊", type = "boolean", scheduleType = "daily"
        });

        var metrics = await ReadJsonAsync(await adminClient.GetAsync("/api/v1/admin/metrics"));

        Assert.True(metrics.GetProperty("totalUsers").GetInt32() >= 2);
        Assert.True(metrics.GetProperty("activeUsers").GetInt32() >= 2);
        Assert.True(metrics.GetProperty("totalHabits").GetInt32() >= 1);
        Assert.True(metrics.GetProperty("newUsersLast30Days").GetInt32() >= 2);
    }
}
