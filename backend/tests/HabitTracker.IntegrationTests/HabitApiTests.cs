using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

[Collection("api")]
public class HabitApiTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestWebAppFactory _factory = new(postgres);

    public void Dispose() => _factory.Dispose();

    private async Task<(HttpClient Client, string Token)> AuthedClientAsync(string? timeZone = null)
    {
        var client = _factory.CreateClient();
        var (auth, _) = await client.RegisterAsync(timeZone: timeZone);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return (client, auth.AccessToken);
    }

    private static object BooleanHabit(string name = "Meditasyon") => new
    {
        name,
        color = "#8b5cf6",
        icon = "🧘",
        type = "boolean",
        scheduleType = "daily"
    };

    private static object QuantityHabit() => new
    {
        name = "Su iç",
        color = "#0ea5e9",
        icon = "💧",
        type = "quantity",
        targetValue = 8,
        unit = "bardak",
        scheduleType = "daily"
    };

    private static object DurationWeekdayHabit() => new
    {
        name = "Koşu",
        color = "#22c55e",
        icon = "🏃",
        type = "duration",
        targetValue = 30,
        scheduleType = "specificWeekdays",
        scheduleDays = new[] { "monday", "wednesday", "friday" }
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    [Fact]
    public async Task CreateHabit_EachType_RoundTripsFields()
    {
        var (client, _) = await AuthedClientAsync();

        var boolean = await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit());
        Assert.Equal(HttpStatusCode.Created, boolean.StatusCode);
        var booleanJson = await ReadJsonAsync(boolean);
        Assert.Equal("boolean", booleanJson.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, booleanJson.GetProperty("targetValue").ValueKind);

        var quantity = await client.PostAsJsonAsync("/api/v1/habits", QuantityHabit());
        Assert.Equal(HttpStatusCode.Created, quantity.StatusCode);
        var quantityJson = await ReadJsonAsync(quantity);
        Assert.Equal(8, quantityJson.GetProperty("targetValue").GetDecimal());
        Assert.Equal("bardak", quantityJson.GetProperty("unit").GetString());

        var duration = await client.PostAsJsonAsync("/api/v1/habits", DurationWeekdayHabit());
        Assert.Equal(HttpStatusCode.Created, duration.StatusCode);
        var durationJson = await ReadJsonAsync(duration);
        Assert.Equal("specificWeekdays", durationJson.GetProperty("scheduleType").GetString());
        Assert.Equal(3, durationJson.GetProperty("scheduleDays").GetArrayLength());
    }

    [Theory]
    [InlineData("quantity")] // missing targetValue + unit
    [InlineData("duration")] // missing targetValue
    public async Task CreateHabit_MissingTarget_Returns400(string type)
    {
        var (client, _) = await AuthedClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Eksik",
            color = "#000000",
            icon = "❓",
            type,
            scheduleType = "daily"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateHabit_InvalidColorAndLongName_Returns400()
    {
        var (client, _) = await AuthedClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = new string('x', 101),
            color = "green",
            icon = "🌿",
            type = "boolean",
            scheduleType = "daily"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name", body);
        Assert.Contains("color", body);
    }

    [Fact]
    public async Task UpdateHabit_ChangesFields()
    {
        var (client, _) = await AuthedClientAsync();
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = created.GetProperty("id").GetString();

        var update = await client.PutAsJsonAsync($"/api/v1/habits/{id}", new
        {
            name = "Akşam meditasyonu",
            color = "#ef4444",
            icon = "🌙",
            scheduleType = "specificWeekdays",
            scheduleDays = new[] { "tuesday", "thursday" }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadJsonAsync(update);
        Assert.Equal("Akşam meditasyonu", updated.GetProperty("name").GetString());
        Assert.Equal(2, updated.GetProperty("scheduleDays").GetArrayLength());
    }

    [Fact]
    public async Task ArchiveAndUnarchive_TogglesAndFiltersList()
    {
        var (client, _) = await AuthedClientAsync();
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = created.GetProperty("id").GetString();

        var archive = await client.PostAsync($"/api/v1/habits/{id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.True((await ReadJsonAsync(archive)).GetProperty("isArchived").GetBoolean());

        var activeList = await ReadJsonAsync(await client.GetAsync("/api/v1/habits"));
        Assert.Equal(0, activeList.GetProperty("totalCount").GetInt32());

        var fullList = await ReadJsonAsync(await client.GetAsync("/api/v1/habits?includeArchived=true"));
        Assert.Equal(1, fullList.GetProperty("totalCount").GetInt32());

        var unarchive = await client.PostAsync($"/api/v1/habits/{id}/unarchive", null);
        Assert.False((await ReadJsonAsync(unarchive)).GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Reorder_PersistsNewOrder()
    {
        var (client, _) = await AuthedClientAsync();
        var first = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit("Birinci")));
        var second = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit("İkinci")));
        var firstId = first.GetProperty("id").GetString()!;
        var secondId = second.GetProperty("id").GetString()!;

        var reorder = await client.PutAsJsonAsync("/api/v1/habits/reorder",
            new { habitIds = new[] { secondId, firstId } });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        var list = await ReadJsonAsync(await client.GetAsync("/api/v1/habits"));
        var names = list.GetProperty("items").EnumerateArray()
            .Select(h => h.GetProperty("name").GetString()).ToList();
        Assert.Equal(["İkinci", "Birinci"], names);
    }

    [Fact]
    public async Task List_Paginates()
    {
        var (client, _) = await AuthedClientAsync();
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit($"Habit {i}"));
        }

        var page = await ReadJsonAsync(await client.GetAsync("/api/v1/habits?page=2&pageSize=2"));
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
    }

    // --- Check-ins ---

    [Fact]
    public async Task CheckIn_BooleanToggle_SetAndUnset()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var on = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = today, value = 1 });
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        var onJson = await ReadJsonAsync(on);
        Assert.True(onJson.GetProperty("checkIn").GetProperty("completed").GetBoolean());
        Assert.True(onJson.GetProperty("currentStreak").GetInt32() >= 1);

        var off = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = today, value = 0 });
        var offJson = await ReadJsonAsync(off);
        Assert.False(offJson.GetProperty("checkIn").GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_QuantityIncrement_AccumulatesAndCompletes()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", QuantityHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        for (var i = 0; i < 7; i++)
        {
            var partial = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins",
                new { date = today, value = 1, increment = true });
            Assert.False((await ReadJsonAsync(partial)).GetProperty("checkIn").GetProperty("completed").GetBoolean());
        }

        var final = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins",
            new { date = today, value = 1, increment = true });
        var finalJson = await ReadJsonAsync(final);
        Assert.Equal(8, finalJson.GetProperty("checkIn").GetProperty("value").GetDecimal());
        Assert.True(finalJson.GetProperty("checkIn").GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_FutureDate_Returns422()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        // Two days ahead is in the future in every timezone.
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");

        var response = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = future, value = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CheckIn_OlderThanSevenDays_Returns422_ButWithinWindowWorks()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();

        var tooOld = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-9).ToString("yyyy-MM-dd");
        var rejected = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = tooOld, value = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);

        var backfill = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-6).ToString("yyyy-MM-dd");
        var accepted = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = backfill, value = 1 });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task CheckIn_ArchivedHabit_Returns422()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        await client.PostAsync($"/api/v1/habits/{id}/archive", null);

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var response = await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = today, value = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Checkins_List_FiltersByRange()
    {
        var (client, _) = await AuthedClientAsync();
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var offset in new[] { 0, -1, -3 })
        {
            await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins",
                new { date = today.AddDays(offset).ToString("yyyy-MM-dd"), value = 1 });
        }

        var response = await client.GetAsync(
            $"/api/v1/habits/{id}/checkins?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var list = await ReadJsonAsync(response);
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task TodayAndWeek_ReflectCheckIns()
    {
        var (client, _) = await AuthedClientAsync(timeZone: "UTC");
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", QuantityHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins", new { date = today, value = 8 });

        var todayView = await ReadJsonAsync(await client.GetAsync("/api/v1/habits/today"));
        var entry = todayView.EnumerateArray().Single();
        Assert.True(entry.GetProperty("completedToday").GetBoolean());
        Assert.Equal(8, entry.GetProperty("todayValue").GetDecimal());
        Assert.Equal(1, entry.GetProperty("currentStreak").GetInt32());

        var week = await ReadJsonAsync(await client.GetAsync("/api/v1/habits/week"));
        var row = week.GetProperty("habits").EnumerateArray().Single();
        var completedCells = row.GetProperty("days").EnumerateArray()
            .Count(c => c.GetProperty("completed").GetBoolean());
        Assert.Equal(1, completedCells);
    }

    [Fact]
    public async Task Stats_ComputesStreakAndRates()
    {
        var (client, _) = await AuthedClientAsync(timeZone: "UTC");
        var habit = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var offset in new[] { 0, -1, -2 })
        {
            await client.PutAsJsonAsync($"/api/v1/habits/{id}/checkins",
                new { date = today.AddDays(offset).ToString("yyyy-MM-dd"), value = 1 });
        }

        var stats = await ReadJsonAsync(await client.GetAsync($"/api/v1/habits/{id}/stats"));
        Assert.Equal(3, stats.GetProperty("currentStreak").GetInt32());
        Assert.Equal(3, stats.GetProperty("longestStreak").GetInt32());
        Assert.Equal("days", stats.GetProperty("streakUnit").GetString());
        Assert.Equal(3, stats.GetProperty("totalCheckIns").GetInt32());
        Assert.Equal(90, stats.GetProperty("heatmap90").GetArrayLength());
        Assert.True(stats.GetProperty("completionRate30").GetDouble() > 0);
    }

    // --- Ownership / IDOR ---

    [Fact]
    public async Task Idor_UserACannotTouchUserBData()
    {
        var (clientA, _) = await AuthedClientAsync();
        var (clientB, _) = await AuthedClientAsync();

        var habit = await ReadJsonAsync(await clientA.PostAsJsonAsync("/api/v1/habits", BooleanHabit()));
        var id = habit.GetProperty("id").GetString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Read, update, archive, check-in, stats — all must 404 for the non-owner.
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/v1/habits/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PutAsJsonAsync($"/api/v1/habits/{id}", new
        {
            name = "Ele geçirildi",
            color = "#000000",
            icon = "☠️",
            scheduleType = "daily"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostAsync($"/api/v1/habits/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PutAsJsonAsync($"/api/v1/habits/{id}/checkins",
            new { date = today, value = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/v1/habits/{id}/checkins")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/v1/habits/{id}/stats")).StatusCode);

        // Reorder with someone else's habit id must fail too.
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PutAsJsonAsync("/api/v1/habits/reorder",
            new { habitIds = new[] { id } })).StatusCode);

        // And user B's list stays empty.
        var listB = await ReadJsonAsync(await clientB.GetAsync("/api/v1/habits"));
        Assert.Equal(0, listB.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Habits_WithoutToken_Return401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/habits")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/habits/today")).StatusCode);
    }
}
