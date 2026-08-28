using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static HabitTracker.IntegrationTests.ApiClientHelpers;

namespace HabitTracker.IntegrationTests;

[Collection("api")]
public class GroupTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestWebAppFactory _factory = new(postgres);

    public void Dispose() => _factory.Dispose();

    private sealed record Person(HttpClient Client, string Email, Guid Id);

    private async Task<Person> CreatePersonAsync()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var (auth, _) = await client.RegisterAsync(email, timeZone: "UTC");
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return new Person(client, email, auth.User.Id);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<Guid> CreateHabitAsync(Person person, string name)
    {
        var created = await person.Client.PostAsJsonAsync("/api/v1/habits", new
        {
            name, color = "#22c55e", icon = "🏃", type = "boolean", scheduleType = "daily"
        });
        return Guid.Parse((await ReadJsonAsync(created)).GetProperty("id").GetString()!);
    }

    private async Task BefriendAsync(Person a, Person b)
    {
        await a.Client.PostAsJsonAsync("/api/v1/friends/requests", new { email = b.Email });
        var requests = await ReadJsonAsync(await b.Client.GetAsync("/api/v1/friends/requests"));
        var requestId = requests.GetProperty("incoming")[0].GetProperty("requestId").GetString();
        await b.Client.PostAsync($"/api/v1/friends/requests/{requestId}/accept", null);
    }

    private static async Task<Guid> CreateGroupAsync(Person owner, Guid habitId, string name = "Sabah koşusu")
    {
        var created = await owner.Client.PostAsJsonAsync("/api/v1/groups", new
        {
            name, description = "Birlikte koşuyoruz", color = "#22c55e", icon = "🏃", habitId
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return Guid.Parse((await ReadJsonAsync(created)).GetProperty("id").GetString()!);
    }

    /// <summary>Owner + friend, both joined, each with their own habit.</summary>
    private async Task<(Person Owner, Person Member, Guid GroupId)> ArrangeJoinedGroupAsync()
    {
        var owner = await CreatePersonAsync();
        var member = await CreatePersonAsync();
        await BefriendAsync(owner, member);

        var groupId = await CreateGroupAsync(owner, await CreateHabitAsync(owner, "Koşu"));
        await owner.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/invitations", new { userId = member.Id });

        var memberHabit = await CreateHabitAsync(member, "Sabah koşusu");
        var join = await member.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/join", new { habitId = memberHabit });
        Assert.Equal(HttpStatusCode.NoContent, join.StatusCode);

        return (owner, member, groupId);
    }

    // --- Creation ---

    [Fact]
    public async Task Create_LinksTheOwnersOwnHabitAndJoinsThemImmediately()
    {
        var owner = await CreatePersonAsync();
        var habitId = await CreateHabitAsync(owner, "Koşu");

        var groupId = await CreateGroupAsync(owner, habitId);
        var detail = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));

        Assert.True(detail.GetProperty("isOwner").GetBoolean());
        var members = detail.GetProperty("members");
        Assert.Equal(1, members.GetArrayLength());
        Assert.Equal("Koşu", members[0].GetProperty("habitName").GetString());
        Assert.True(members[0].GetProperty("isYou").GetBoolean());
    }

    [Fact]
    public async Task Create_WithSomeoneElsesHabit_Returns404()
    {
        var owner = await CreatePersonAsync();
        var stranger = await CreatePersonAsync();
        var strangersHabit = await CreateHabitAsync(stranger, "Gizli");

        var response = await owner.Client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Ele geçirme", color = "#000000", icon = "🏃", habitId = strangersHabit
        });

        // Linking a habit you do not own would expose its progress to the group.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidatesItsInput()
    {
        var owner = await CreatePersonAsync();
        var habitId = await CreateHabitAsync(owner, "Koşu");

        var response = await owner.Client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "", color = "yeşil", icon = "🏃", habitId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Invitations ---

    [Fact]
    public async Task Invite_OnlyWorksForFriends()
    {
        var owner = await CreatePersonAsync();
        var stranger = await CreatePersonAsync();
        var groupId = await CreateGroupAsync(owner, await CreateHabitAsync(owner, "Koşu"));

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/invitations", new { userId = stranger.Id });

        // A group must not become a way into a stranger's app.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Invite_OnlyTheOwnerMayInvite()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();
        var outsider = await CreatePersonAsync();
        await BefriendAsync(member, outsider);

        // A joined member is not an owner: inviting is the owner's call alone.
        var response = await member.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/invitations", new { userId = outsider.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(owner.Id, outsider.Id);
    }

    [Fact]
    public async Task Invitation_ShowsInTheInviteesListButRevealsNoProgress()
    {
        var owner = await CreatePersonAsync();
        var invitee = await CreatePersonAsync();
        await BefriendAsync(owner, invitee);
        var groupId = await CreateGroupAsync(owner, await CreateHabitAsync(owner, "Koşu"));
        await owner.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/invitations", new { userId = invitee.Id });

        var groups = await ReadJsonAsync(await invitee.Client.GetAsync("/api/v1/groups"));
        Assert.Equal(1, groups.GetArrayLength());
        Assert.False(groups[0].GetProperty("joined").GetBoolean());

        // An outstanding invitation is not consent, so members stay hidden.
        var detail = await invitee.Client.GetAsync($"/api/v1/groups/{groupId}");
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
    }

    [Fact]
    public async Task Join_RequiresAHabitTheJoinerOwns()
    {
        var owner = await CreatePersonAsync();
        var invitee = await CreatePersonAsync();
        await BefriendAsync(owner, invitee);
        var groupId = await CreateGroupAsync(owner, await CreateHabitAsync(owner, "Koşu"));
        await owner.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/invitations", new { userId = invitee.Id });

        var ownersHabit = await CreateHabitAsync(owner, "Sadece bende");
        var response = await invitee.Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/join", new { habitId = ownersHabit });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Decline_RemovesTheInvitationEntirely()
    {
        var owner = await CreatePersonAsync();
        var invitee = await CreatePersonAsync();
        await BefriendAsync(owner, invitee);
        var groupId = await CreateGroupAsync(owner, await CreateHabitAsync(owner, "Koşu"));
        await owner.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/invitations", new { userId = invitee.Id });

        Assert.Equal(HttpStatusCode.NoContent,
            (await invitee.Client.PostAsync($"/api/v1/groups/{groupId}/decline", null)).StatusCode);

        Assert.Empty((await ReadJsonAsync(await invitee.Client.GetAsync("/api/v1/groups"))).EnumerateArray());
    }

    // --- Membership and visibility ---

    [Fact]
    public async Task JoinedMembers_SeeEachOthersGroupProgress()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var memberGroups = await ReadJsonAsync(await member.Client.GetAsync("/api/v1/habits"));
        var memberHabitId = memberGroups.GetProperty("items")[0].GetProperty("id").GetString();
        await member.Client.PutAsJsonAsync($"/api/v1/habits/{memberHabitId}/checkins", new { date = today, value = 1 });

        var detail = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));
        var members = detail.GetProperty("members");

        Assert.Equal(2, members.GetArrayLength());
        var memberEntry = members.EnumerateArray().Single(m => m.GetProperty("email").GetString() == member.Email);
        Assert.True(memberEntry.GetProperty("completedToday").GetBoolean());
        Assert.Equal(1, memberEntry.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, memberEntry.GetProperty("completionsLast7Days").GetInt32());
    }

    [Fact]
    public async Task NonMember_CannotReadTheGroupAtAll()
    {
        var (_, _, groupId) = await ArrangeJoinedGroupAsync();
        var outsider = await CreatePersonAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/groups/{groupId}")).StatusCode);
        Assert.Empty((await ReadJsonAsync(await outsider.Client.GetAsync("/api/v1/groups"))).EnumerateArray());
    }

    [Fact]
    public async Task GroupMembership_NeverOpensTheHabitEndpoints()
    {
        var (owner, member, _) = await ArrangeJoinedGroupAsync();

        var memberHabits = await ReadJsonAsync(await member.Client.GetAsync("/api/v1/habits"));
        var memberHabitId = memberHabits.GetProperty("items")[0].GetProperty("id").GetString();

        // Group progress is a projection, not access to the habit itself.
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/habits/{memberHabitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/habits/{memberHabitId}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.PutAsJsonAsync($"/api/v1/habits/{memberHabitId}/checkins",
                new { date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), value = 1 })).StatusCode);
    }

    [Fact]
    public async Task GroupSharing_IsIndependentOfTheFriendListFlag()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();

        // The member never enabled friend-list sharing...
        var friends = await ReadJsonAsync(await owner.Client.GetAsync("/api/v1/friends"));
        Assert.False(friends[0].GetProperty("sharingEnabled").GetBoolean());

        // ...yet joining the group is its own, narrower consent.
        var detail = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));
        Assert.Equal(2, detail.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task PendingInvitees_AreVisibleOnlyToTheOwner()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();
        var third = await CreatePersonAsync();
        await BefriendAsync(owner, third);
        await owner.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/invitations", new { userId = third.Id });

        var ownerView = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));
        var memberView = await ReadJsonAsync(await member.Client.GetAsync($"/api/v1/groups/{groupId}"));

        Assert.Equal(1, ownerView.GetProperty("pendingInvitees").GetArrayLength());
        Assert.Equal(0, memberView.GetProperty("pendingInvitees").GetArrayLength());
    }

    // --- Leaving and removal ---

    [Fact]
    public async Task Leave_EndsVisibilityBothWays()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();

        Assert.Equal(HttpStatusCode.NoContent,
            (await member.Client.PostAsync($"/api/v1/groups/{groupId}/leave", null)).StatusCode);

        Assert.Empty((await ReadJsonAsync(await member.Client.GetAsync("/api/v1/groups"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.Client.GetAsync($"/api/v1/groups/{groupId}")).StatusCode);

        var ownerView = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));
        Assert.Equal(1, ownerView.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Owner_CannotLeaveButCanDeleteTheGroup()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await owner.Client.PostAsync($"/api/v1/groups/{groupId}/leave", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/groups/{groupId}")).StatusCode);

        Assert.Empty((await ReadJsonAsync(await member.Client.GetAsync("/api/v1/groups"))).EnumerateArray());
    }

    [Fact]
    public async Task OnlyTheOwnerMayRemoveMembersOrDeleteTheGroup()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await member.Client.DeleteAsync($"/api/v1/groups/{groupId}/members/{owner.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.Client.DeleteAsync($"/api/v1/groups/{groupId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/groups/{groupId}/members/{member.Id}")).StatusCode);
    }

    [Fact]
    public async Task ChangeHabit_SwapsTheTrackedHabit_ButOnlyToOneYouOwn()
    {
        var (_, member, groupId) = await ArrangeJoinedGroupAsync();
        var replacement = await CreateHabitAsync(member, "Akşam koşusu");
        var stranger = await CreatePersonAsync();
        var strangersHabit = await CreateHabitAsync(stranger, "Başkasının");

        Assert.Equal(HttpStatusCode.NoContent,
            (await member.Client.PutAsJsonAsync($"/api/v1/groups/{groupId}/habit", new { habitId = replacement })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.Client.PutAsJsonAsync($"/api/v1/groups/{groupId}/habit", new { habitId = strangersHabit })).StatusCode);

        var detail = await ReadJsonAsync(await member.Client.GetAsync($"/api/v1/groups/{groupId}"));
        var self = detail.GetProperty("members").EnumerateArray().Single(m => m.GetProperty("isYou").GetBoolean());
        Assert.Equal("Akşam koşusu", self.GetProperty("habitName").GetString());
    }

    [Fact]
    public async Task DeletingAnAccount_RemovesItFromGroups()
    {
        var (owner, member, groupId) = await ArrangeJoinedGroupAsync();

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password = "Str0ngPass!x" })
        };
        delete.Headers.Authorization = member.Client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.NoContent, (await member.Client.SendAsync(delete)).StatusCode);

        var detail = await ReadJsonAsync(await owner.Client.GetAsync($"/api/v1/groups/{groupId}"));
        Assert.Equal(1, detail.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task GroupEndpoints_RequireAuthentication()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/groups/{Guid.NewGuid()}")).StatusCode);
    }
}
