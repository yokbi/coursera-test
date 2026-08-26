using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

[Collection("api")]
public class FriendTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestWebAppFactory _factory = new(postgres);

    public void Dispose() => _factory.Dispose();

    private sealed record Person(HttpClient Client, string Email, Guid Id);

    private async Task<Person> CreatePersonAsync(string timeZone = "UTC")
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: timeZone);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return new Person(client, email, auth.User.Id);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static Task<HttpResponseMessage> SendRequestAsync(Person from, Person to) =>
        from.Client.PostAsJsonAsync("/api/v1/friends/requests", new { email = to.Email });

    /// <summary>Links two accounts: request then accept.</summary>
    private async Task BefriendAsync(Person a, Person b)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await SendRequestAsync(a, b)).StatusCode);

        var requests = await ReadJsonAsync(await b.Client.GetAsync("/api/v1/friends/requests"));
        var requestId = requests.GetProperty("incoming")[0].GetProperty("requestId").GetString();

        Assert.Equal(HttpStatusCode.NoContent,
            (await b.Client.PostAsync($"/api/v1/friends/requests/{requestId}/accept", null)).StatusCode);
    }

    private static async Task AddCompletedHabitAsync(Person person, string name, string date)
    {
        var created = await person.Client.PostAsJsonAsync("/api/v1/habits", new
        {
            name, color = "#22c55e", icon = "✅", type = "boolean", scheduleType = "daily"
        });
        var habitId = (await ReadJsonAsync(created)).GetProperty("id").GetString();
        await person.Client.PutAsJsonAsync($"/api/v1/habits/{habitId}/checkins", new { date, value = 1 });
    }

    private static Task EnableSharingAsync(Person person) =>
        person.Client.PutAsJsonAsync("/api/v1/friends/sharing", new { shareStreaks = true });

    // --- Requests ---

    [Fact]
    public async Task Request_ThenAccept_MakesBothSidesFriends()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();

        await BefriendAsync(alice, bob);

        var aliceFriends = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"));
        var bobFriends = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends"));

        Assert.Equal(bob.Email, aliceFriends[0].GetProperty("email").GetString());
        Assert.Equal(alice.Email, bobFriends[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task Request_ToUnknownAddress_LooksIdenticalToARealOne()
    {
        var alice = await CreatePersonAsync();

        var unknown = await alice.Client.PostAsJsonAsync("/api/v1/friends/requests",
            new { email = "nobody-" + Guid.NewGuid().ToString("N") + "@test.local" });
        var real = await SendRequestAsync(alice, await CreatePersonAsync());

        // Identical responses: the endpoint must not reveal which emails exist.
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(real.StatusCode, unknown.StatusCode);

        // And no phantom request was created for the unknown address.
        var outgoing = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends/requests"));
        Assert.Equal(1, outgoing.GetProperty("outgoing").GetArrayLength());
    }

    [Fact]
    public async Task Request_ToSelf_IsSilentlyIgnored()
    {
        var alice = await CreatePersonAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/v1/friends/requests", new { email = alice.Email });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var requests = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends/requests"));
        Assert.Equal(0, requests.GetProperty("outgoing").GetArrayLength());
        Assert.Empty((await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task MutualRequests_LinkImmediatelyWithoutAnAccept()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();

        await SendRequestAsync(alice, bob);
        // Bob asks back rather than pressing accept — that is consent too.
        await SendRequestAsync(bob, alice);

        var bobFriends = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends"));
        Assert.Equal(1, bobFriends.GetArrayLength());
        Assert.Equal(alice.Email, bobFriends[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task DuplicateRequest_DoesNotCreateASecondRow()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();

        await SendRequestAsync(alice, bob);
        await SendRequestAsync(alice, bob);

        var incoming = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends/requests"));
        Assert.Equal(1, incoming.GetProperty("incoming").GetArrayLength());
    }

    [Fact]
    public async Task Requester_CannotAcceptTheirOwnRequest()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await SendRequestAsync(alice, bob);

        var outgoing = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends/requests"));
        var requestId = outgoing.GetProperty("outgoing")[0].GetProperty("requestId").GetString();

        // Otherwise anyone could friend anyone unilaterally.
        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.Client.PostAsync($"/api/v1/friends/requests/{requestId}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task Stranger_CannotAcceptSomeoneElsesRequest()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        var mallory = await CreatePersonAsync();
        await SendRequestAsync(alice, bob);

        var incoming = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends/requests"));
        var requestId = incoming.GetProperty("incoming")[0].GetProperty("requestId").GetString();

        Assert.Equal(HttpStatusCode.NotFound,
            (await mallory.Client.PostAsync($"/api/v1/friends/requests/{requestId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await mallory.Client.PostAsync($"/api/v1/friends/requests/{requestId}/decline", null)).StatusCode);
    }

    [Fact]
    public async Task Decline_LeavesNoFriendship_ButAllowsAskingAgainLater()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await SendRequestAsync(alice, bob);

        var incoming = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends/requests"));
        var requestId = incoming.GetProperty("incoming")[0].GetProperty("requestId").GetString();
        await bob.Client.PostAsync($"/api/v1/friends/requests/{requestId}/decline", null);

        Assert.Empty((await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"))).EnumerateArray());

        // A refusal is not permanent.
        await SendRequestAsync(alice, bob);
        var reopened = await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends/requests"));
        Assert.Equal(1, reopened.GetProperty("incoming").GetArrayLength());
    }

    // --- Privacy ---

    [Fact]
    public async Task Friend_WithoutConsent_ExposesNoHabitData()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await AddCompletedHabitAsync(bob, "Meditasyon", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        await BefriendAsync(alice, bob);

        var friends = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"));
        var bobEntry = friends[0];

        Assert.False(bobEntry.GetProperty("sharingEnabled").GetBoolean());
        // Sharing defaults to off, so every stat must be absent.
        foreach (var field in new[] { "activeHabits", "bestStreak", "bestStreakHabitName", "completedToday" })
        {
            Assert.Equal(JsonValueKind.Null, bobEntry.GetProperty(field).ValueKind);
        }
    }

    [Fact]
    public async Task Friend_WithConsent_SharesStreakSummaryOnly()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        await AddCompletedHabitAsync(bob, "Meditasyon", today);
        await EnableSharingAsync(bob);
        await BefriendAsync(alice, bob);

        var friends = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"));
        var bobEntry = friends[0];

        Assert.True(bobEntry.GetProperty("sharingEnabled").GetBoolean());
        Assert.Equal(1, bobEntry.GetProperty("activeHabits").GetInt32());
        Assert.Equal(1, bobEntry.GetProperty("bestStreak").GetInt32());
        Assert.Equal("Meditasyon", bobEntry.GetProperty("bestStreakHabitName").GetString());
        Assert.Equal(1, bobEntry.GetProperty("completedToday").GetInt32());
    }

    [Fact]
    public async Task RevokingConsent_ImmediatelyHidesTheData()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await AddCompletedHabitAsync(bob, "Meditasyon", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        await EnableSharingAsync(bob);
        await BefriendAsync(alice, bob);

        await bob.Client.PutAsJsonAsync("/api/v1/friends/sharing", new { shareStreaks = false });

        var friends = await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"));
        Assert.False(friends[0].GetProperty("sharingEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, friends[0].GetProperty("bestStreak").ValueKind);
    }

    [Fact]
    public async Task Stranger_SeesNothing_EvenWhenSharingIsOn()
    {
        var stranger = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await AddCompletedHabitAsync(bob, "Meditasyon", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        await EnableSharingAsync(bob);

        var friends = await ReadJsonAsync(await stranger.Client.GetAsync("/api/v1/friends"));
        Assert.Empty(friends.EnumerateArray());
    }

    [Fact]
    public async Task PendingRequest_SharesNothingBeforeItIsAccepted()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await AddCompletedHabitAsync(bob, "Meditasyon", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        await EnableSharingAsync(bob);
        await SendRequestAsync(alice, bob);

        // A request in flight is not consent.
        Assert.Empty((await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task Friendship_NeverExposesHabitEndpointsToTheFriend()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await EnableSharingAsync(bob);
        await BefriendAsync(alice, bob);

        var created = await bob.Client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Gizli", color = "#000000", icon = "🔒", type = "boolean", scheduleType = "daily"
        });
        var habitId = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        // Sharing is a summary, not access: the owner-scoped endpoints stay closed.
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.GetAsync($"/api/v1/habits/{habitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.GetAsync($"/api/v1/habits/{habitId}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.GetAsync($"/api/v1/habits/{habitId}/checkins")).StatusCode);
    }

    // --- Removal ---

    [Fact]
    public async Task Remove_CutsVisibilityForBothSides()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await EnableSharingAsync(bob);
        await BefriendAsync(alice, bob);

        Assert.Equal(HttpStatusCode.NoContent,
            (await alice.Client.DeleteAsync($"/api/v1/friends/{bob.Id}")).StatusCode);

        Assert.Empty((await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"))).EnumerateArray());
        Assert.Empty((await ReadJsonAsync(await bob.Client.GetAsync("/api/v1/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task Remove_ANonFriend_Returns404()
    {
        var alice = await CreatePersonAsync();
        var stranger = await CreatePersonAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.Client.DeleteAsync($"/api/v1/friends/{stranger.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeletingAnAccount_RemovesItFromFriendLists()
    {
        var alice = await CreatePersonAsync();
        var bob = await CreatePersonAsync();
        await BefriendAsync(alice, bob);

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password = "Str0ngPass!x" })
        };
        delete.Headers.Authorization = bob.Client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.NoContent, (await bob.Client.SendAsync(delete)).StatusCode);

        Assert.Empty((await ReadJsonAsync(await alice.Client.GetAsync("/api/v1/friends"))).EnumerateArray());
    }

    [Fact]
    public async Task FriendEndpoints_RequireAuthentication()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/friends")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/friends/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/v1/friends/requests", new { email = "x@y.z" })).StatusCode);
    }

    [Fact]
    public async Task SharingToggle_IsReflectedOnTheProfile()
    {
        var alice = await CreatePersonAsync();

        var enabled = await alice.Client.PutAsJsonAsync("/api/v1/friends/sharing", new { shareStreaks = true });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.True((await enabled.Content.ReadFromJsonAsync<UserInfoDto>(Json))!.ShareStreaksWithFriends);

        var profile = await alice.Client.GetFromJsonAsync<UserInfoDto>("/api/v1/me", Json);
        Assert.True(profile!.ShareStreaksWithFriends);
    }
}
