using System.Net;
using System.Net.Http.Json;
using HabitTracker.Application.Common.Interfaces;
using HabitTracker.Application.Reminders;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

public class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Lets a test place "now" wherever it needs, so reminder windows are deterministic.</summary>
public class SettableClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}

[Collection("api")]
public class ReminderTests(PostgresFixture postgres) : IDisposable
{
    private readonly CapturingEmailSender _email = new();
    private readonly SettableClock _clock = new();
    private readonly TestWebAppFactory _base = new(postgres);
    private WebApplicationFactory<Program>? _app;

    public void Dispose()
    {
        _app?.Dispose();
        _base.Dispose();
    }

    private WebApplicationFactory<Program> App => _app ??= _base.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(_email);
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(_clock);
        }));

    private async Task<int> RunSweepAsync()
    {
        using var scope = App.Services.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
        return await reminders.ProcessDueRemindersAsync();
    }

    private async Task<Guid> ResolveUserIdAsync(string email)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    /// <summary>Registers a user in UTC with one daily boolean habit and reminders on.</summary>
    private async Task<(HttpClient Client, string Email)> ArrangeUserWithHabitAsync(int reminderHour = 20)
    {
        var client = App.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: "UTC");
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Meditasyon",
            color = "#8b5cf6",
            icon = "🧘",
            type = "boolean",
            scheduleType = "daily"
        });

        var settings = await client.PutAsJsonAsync("/api/v1/reminders/settings",
            new { enabled = true, hour = reminderHour });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        return (client, email);
    }

    [Fact]
    public async Task Sweep_SendsOnceAfterThePreferredHour_AndIsIdempotent()
    {
        var (_, email) = await ArrangeUserWithHabitAsync(reminderHour: 20);

        // The sweep is global, so every assertion targets this test's own recipient
        // rather than the batch count, which other tests' users also contribute to.

        // 19:00 UTC — before the user's hour, nothing goes out.
        _clock.UtcNow = new DateTime(2026, 7, 9, 19, 0, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.DoesNotContain(_email.Sent, m => m.To == email);

        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Single(_email.Sent, m => m.To == email);

        // A second sweep the same local day must not send again.
        _clock.UtcNow = new DateTime(2026, 7, 9, 22, 0, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Single(_email.Sent, m => m.To == email);

        // The next local day it sends again.
        _clock.UtcNow = new DateTime(2026, 7, 10, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Equal(2, _email.Sent.Count(m => m.To == email));
    }

    [Fact]
    public async Task Sweep_RespectsTheUsersOwnTimezone()
    {
        var client = App.CreateClient();
        var email = UniqueEmail();
        // Istanbul is UTC+3, so 20:00 local is 17:00 UTC.
        var (auth, _) = await client.RegisterAsync(email, timeZone: "Europe/Istanbul");
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Kitap oku", color = "#f59e0b", icon = "📚", type = "boolean", scheduleType = "daily"
        });
        await client.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 20 });

        // 16:00 UTC = 19:00 Istanbul — too early.
        _clock.UtcNow = new DateTime(2026, 7, 9, 16, 0, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.DoesNotContain(_email.Sent, m => m.To == email);

        // 17:00 UTC = 20:00 Istanbul — due.
        _clock.UtcNow = new DateTime(2026, 7, 9, 17, 0, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Contains(_email.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Sweep_SkipsUsersWhoAlreadyCompletedEverything()
    {
        var (client, email) = await ArrangeUserWithHabitAsync();
        var habits = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/habits");
        var habitId = habits.GetProperty("items")[0].GetProperty("id").GetString();

        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await client.PutAsJsonAsync($"/api/v1/habits/{habitId}/checkins",
            new { date = "2026-07-09", value = 1 });

        await RunSweepAsync();

        Assert.DoesNotContain(_email.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Sweep_IgnoresUsersWhoNeverOptedIn()
    {
        var client = App.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: "UTC");
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Meditasyon", color = "#8b5cf6", icon = "🧘", type = "boolean", scheduleType = "daily"
        });

        _clock.UtcNow = new DateTime(2026, 7, 9, 23, 0, 0, DateTimeKind.Utc);
        await RunSweepAsync();

        // Reminders are opt-in; a fresh account is never mailed.
        Assert.DoesNotContain(_email.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Email_NamesThePendingHabitAndCarriesAnUnsubscribeLink()
    {
        var (_, email) = await ArrangeUserWithHabitAsync();

        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();

        var message = Assert.Single(_email.Sent, m => m.To == email);
        Assert.Contains("Meditasyon", message.TextBody);
        Assert.Contains(ReminderService.UnsubscribePath, message.TextBody);
    }

    [Fact]
    public async Task UnsubscribeLink_TurnsRemindersOff_WithoutASession()
    {
        var (_, email) = await ArrangeUserWithHabitAsync();
        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        var message = Assert.Single(_email.Sent, m => m.To == email);

        // Pull the link out of the mail exactly as a reader would click it, then keep
        // the path + query so the in-memory test client can request it.
        var start = message.TextBody.IndexOf(ReminderService.UnsubscribePath, StringComparison.Ordinal);
        var link = message.TextBody[start..].Split('\n')[0].Trim();

        // Anonymous client: no Authorization header, no cookies.
        var anonymous = App.CreateClient();
        var response = await anonymous.GetAsync(link);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The next day's sweep now skips this user entirely.
        _clock.UtcNow = new DateTime(2026, 7, 10, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Single(_email.Sent, m => m.To == email);
    }

    [Fact]
    public async Task UnsubscribeLink_WithATamperedToken_IsRejected()
    {
        var (_, email) = await ArrangeUserWithHabitAsync();
        var userId = await ResolveUserIdAsync(email);
        var anonymous = App.CreateClient();

        // Right user id, forged signature.
        var forged = $"{ReminderService.UnsubscribePath}?token={userId:N}.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync(forged)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.GetAsync($"{ReminderService.UnsubscribePath}?token=garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.GetAsync(ReminderService.UnsubscribePath)).StatusCode);

        // Reminders stay on: a rejected token changes nothing.
        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();
        Assert.Single(_email.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Settings_RequireAuthentication_AndValidateTheHour()
    {
        var anonymous = App.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 9 })).StatusCode);

        var client = App.CreateClient();
        var (auth, _) = await client.RegisterAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 24 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = -1 })).StatusCode);

        var ok = await client.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 7 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var profile = await ok.Content.ReadFromJsonAsync<UserInfoDto>(Json);
        Assert.True(profile!.RemindersEnabled);
        Assert.Equal(7, profile.ReminderHour);
    }

    [Fact]
    public async Task WeeklyQuotaHabit_StopsNagging_OnceTheQuotaIsMet()
    {
        var client = App.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: "UTC");
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var created = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Yüzme", color = "#06b6d4", icon = "🏊",
            type = "boolean", scheduleType = "timesPerWeek", timesPerWeek = 2
        });
        var habitId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetString();
        await client.PutAsJsonAsync("/api/v1/reminders/settings", new { enabled = true, hour = 20 });

        // 2026-07-06 is the Monday of that week; log one of the two sessions.
        _clock.UtcNow = new DateTime(2026, 7, 9, 20, 30, 0, DateTimeKind.Utc);
        await client.PutAsJsonAsync($"/api/v1/habits/{habitId}/checkins",
            new { date = "2026-07-08", value = 1 });

        // Quota still unmet (1 of 2), so a reminder is due even though nothing is due "today".
        await RunSweepAsync();
        Assert.Single(_email.Sent, m => m.To == email);

        // Meet the quota, then move to the next day: no more nagging this week.
        await client.PutAsJsonAsync($"/api/v1/habits/{habitId}/checkins",
            new { date = "2026-07-09", value = 1 });
        _clock.UtcNow = new DateTime(2026, 7, 10, 20, 30, 0, DateTimeKind.Utc);
        await RunSweepAsync();

        Assert.Single(_email.Sent, m => m.To == email);
    }
}
