using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.AI;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The calendar administration, event, category and feed address AI tools ask what the calendar pages ask, run the
/// pages' rules, and never return a feed token, a subscription address or an answer token.
/// </summary>
public sealed class CalendarManagementAIToolTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;
    private static readonly string _manageAll = CalendarPermissions.ManageAllCalendars.Name;

    // The harness clock says 12:00 UTC on 2 November 2026.
    private static readonly DateTime _start = new(2026, 11, 4, 10, 0, 0);

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var name in CalendarAIToolRegistrationTests.NewTools.Select(tool => tool.Name))
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesSomeoneWhoIsNotSignedIn_OrMayNotUseCalendars(string toolName)
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects");
        var arguments = new Dictionary<string, object> { ["calendarId"] = calendar.ItemId, ["name"] = "Changed", ["action"] = "remove", ["enabled"] = false };

        // Act
        var anonymous = await InvokeAsync(harness, new ClaimsPrincipal(new ClaimsIdentity()), CalendarAIToolRegistrationTests.Create(toolName), arguments);
        var stranger = await InvokeAsync(harness, CalendarTestHarness.User("stranger"), CalendarAIToolRegistrationTests.Create(toolName), arguments);
        var unchanged = await FindCalendarAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal("The calendar can only be used by a signed-in person.", anonymous);
        Assert.Equal("The user isn't allowed to use calendars.", stranger);
        Assert.Equal("Projects", unchanged.Name);
    }

    [Fact]
    public async Task GetCalendar_ShowsSharingOnlyToManagers_AndNeverTheFeedOrSubscriptionAddress()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Partner Events", c =>
        {
            c.FeedToken = "feed-token-0123456789";
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Read });
            c.Put(new CalendarSubscription
            {
                Provider = "ics",
                Url = "https://user:pw@calendars.example.com/private-abc123/basic.ics",
                LastError = "GET https://user:pw@calendars.example.com/private-abc123/basic.ics returned 404.",
                SyncToken = "sync-token-xyz",
            });
        });

        // Act
        var admin = await InvokeAsync(harness, Admin(), new GetCalendarTool(), new() { ["calendarId"] = calendar.ItemId });
        var reader = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new GetCalendarTool(), new() { ["calendarId"] = calendar.ItemId });
        var stranger = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new GetCalendarTool(), new() { ["calendarId"] = calendar.ItemId });
        var missing = await InvokeAsync(harness, Admin(), new GetCalendarTool(), new() { ["calendarId"] = "missing" });
        var adminJson = Parse(admin);
        var readerJson = Parse(reader);

        // Assert
        Assert.True(adminJson.GetProperty("hasFeedAddress").GetBoolean());
        Assert.Equal("jane", adminJson.GetProperty("shares")[0].GetProperty("id").GetString());
        Assert.Equal("https://calendars.example.com", adminJson.GetProperty("subscription").GetProperty("addressHost").GetString());
        Assert.DoesNotContain("feed-token", admin.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-abc123", admin.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sync-token", admin.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("pw@", admin.ToString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, readerJson.GetProperty("shares").ValueKind);
        Assert.Equal(JsonValueKind.Null, readerJson.GetProperty("subscription").ValueKind);
        Assert.Equal(CalendarToolSupport.CalendarNotFound, stranger);
        Assert.Equal(CalendarToolSupport.CalendarNotFound, missing);
    }

    [Fact]
    public async Task SaveCalendar_CreatesARoomWithItsDetails_AndRefusesWhatTheEditorWouldNotAccept()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var created = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new()
        {
            ["type"] = CalendarTestHarness.Room,
            ["name"] = "Boardroom",
            ["timeZoneId"] = "America/New_York",
            ["seats"] = 12,
            ["location"] = "Floor 3",
        });
        var seatsOnGeneral = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["type"] = CalendarTestHarness.General, ["name"] = "Ops", ["seats"] = 4 });
        var unknownRole = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["type"] = CalendarTestHarness.Team, ["name"] = "Ghosts", ["ownerId"] = "Ghost" });
        var team = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["type"] = CalendarTestHarness.Team, ["name"] = "Sales team", ["ownerId"] = "sales" });
        var notAllowed = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new SaveCalendarTool(), new() { ["type"] = CalendarTestHarness.General, ["name"] = "Mine" });
        var unknownType = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["type"] = "Spaceship", ["name"] = "Enterprise" });
        var roomId = Parse(created).GetProperty("calendar").GetProperty("id").GetString();
        var room = await FindCalendarAsync(harness, roomId);

        // Assert
        Assert.Equal("Boardroom", room.Name);
        Assert.Equal("America/New_York", room.TimeZoneId);
        Assert.Equal(12, room.GetOrCreate<ResourceCalendarSettings>().Seats);
        Assert.Equal("Floor 3", room.GetOrCreate<ResourceCalendarSettings>().Location);
        Assert.Equal("seats applies only to Room and Equipment calendars.", seatsOnGeneral);
        Assert.Equal("There is no role 'Ghost' a team calendar can belong to.", unknownRole);
        Assert.Equal("Sales", Parse(team).GetProperty("calendar").GetProperty("ownerId").GetString());
        Assert.Equal("The user can't create General calendars.", notAllowed);
        Assert.StartsWith("There is no calendar type 'Spaceship'.", unknownType.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCalendar_UpdateNeedsManageAccess_AndKeepsFieldsLeftOut()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects", c =>
        {
            c.Description = "Team projects";
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write });
        });

        // Act
        var jane = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new SaveCalendarTool(), new() { ["calendarId"] = calendar.ItemId, ["name"] = "Taken" });
        var badZone = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["calendarId"] = calendar.ItemId, ["timeZoneId"] = "Mars/Olympus" });
        var admin = await InvokeAsync(harness, Admin(), new SaveCalendarTool(), new() { ["calendarId"] = calendar.ItemId, ["name"] = "Projects 2027", ["limitOverlaps"] = true, ["capacity"] = 2 });
        var saved = await FindCalendarAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal("The user can't change that calendar: it needs the right to manage it.", jane);
        Assert.StartsWith("'Mars/Olympus' is not a time zone.", badZone.ToString(), StringComparison.Ordinal);
        Assert.False(Parse(admin).GetProperty("created").GetBoolean());
        Assert.Equal("Projects 2027", saved.Name);
        Assert.Equal("Team projects", saved.Description);
        Assert.Equal(CalendarConflictPolicy.LimitConcurrent, saved.ConflictPolicy);
        Assert.Equal(2, saved.Capacity);
        Assert.Single(saved.Shares);
    }

    [Fact]
    public async Task ShareCalendar_ReplacesTheShares_WithKnownUsersAndRolesOnly()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Marketing", c =>
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "john", Access = CalendarAccessLevel.Read }));
        var shares = new object[]
        {
            new Dictionary<string, object> { ["kind"] = "Role", ["id"] = "sales", ["access"] = "Write" },
            new Dictionary<string, object> { ["kind"] = "User", ["id"] = "jane", ["access"] = "Read" },
        };

        // Act
        var john = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new ShareCalendarTool(), new() { ["calendarId"] = calendar.ItemId, ["shares"] = shares });
        var ghost = await InvokeAsync(harness, Admin(), new ShareCalendarTool(), new()
        {
            ["calendarId"] = calendar.ItemId,
            ["shares"] = new object[] { new Dictionary<string, object> { ["kind"] = "Role", ["id"] = "Anonymous", ["access"] = "Read" } },
        });
        var nobody = await InvokeAsync(harness, Admin(), new ShareCalendarTool(), new()
        {
            ["calendarId"] = calendar.ItemId,
            ["shares"] = new object[] { new Dictionary<string, object> { ["kind"] = "User", ["id"] = "priya", ["access"] = "Read" } },
        });
        var unchanged = await FindCalendarAsync(harness, calendar.ItemId);
        var admin = await InvokeAsync(harness, Admin(), new ShareCalendarTool(), new() { ["calendarId"] = calendar.ItemId, ["shares"] = shares });
        var saved = await FindCalendarAsync(harness, calendar.ItemId);

        // Assert
        Assert.Equal("The user can't change who that calendar is shared with: it needs the right to manage it.", john);
        Assert.Equal("There is no role 'Anonymous' a calendar can be shared with.", ghost);
        Assert.StartsWith("There is no user with the ID 'priya'.", nobody.ToString(), StringComparison.Ordinal);
        Assert.Equal("john", Assert.Single(unchanged.Shares).PrincipalId);
        Assert.Equal("Manage", Parse(admin).GetProperty("yourAccess").GetString());
        Assert.Equal(2, saved.Shares.Count);
        Assert.Contains(saved.Shares, share => share.PrincipalKind == CalendarPrincipalKind.Role && share.PrincipalId == "Sales" && share.Access == CalendarAccessLevel.Write);
        Assert.Contains(saved.Shares, share => share.PrincipalKind == CalendarPrincipalKind.User && share.PrincipalId == "jane" && share.Access == CalendarAccessLevel.Read);
    }

    [Fact]
    public async Task DeleteCalendar_NeedsManageAccess_AndLeavesModuleCalendarsAlone()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Old projects", c =>
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write }));
        var managed = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Appointments", c => c.Source = "Scheduling");

        // Act
        var jane = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new DeleteCalendarTool(), new() { ["calendarId"] = calendar.ItemId });
        var module = await InvokeAsync(harness, Admin(), new DeleteCalendarTool(), new() { ["calendarId"] = managed.ItemId });
        var admin = await InvokeAsync(harness, Admin(), new DeleteCalendarTool(), new() { ["calendarId"] = calendar.ItemId });

        // Assert
        Assert.Equal("The user can't delete that calendar: it needs the right to manage it.", jane);
        Assert.Equal("The calendar is managed by Scheduling and can't be deleted here.", module);
        Assert.True(Parse(admin).GetProperty("deleted").GetBoolean());
        Assert.Null(await FindCalendarAsync(harness, calendar.ItemId));
        Assert.NotNull(await FindCalendarAsync(harness, managed.ItemId));
    }

    [Fact]
    public async Task GetCalendarEvent_NeedsTheRightToSeeIt_AndNeverReturnsAnAnswerToken()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects");
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1), e =>
            e.Attendees.Add(new CalendarAttendee { UserId = "jane", Name = "jane", Email = "jane@example.com", ResponseToken = "answer-token-999" }));

        // Act
        var admin = await InvokeAsync(harness, CalendarTestHarness.User("admin", CalendarPermissions.ViewAllCalendars.Name), new GetCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId });
        var john = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new GetCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId });
        var missing = await InvokeAsync(harness, Admin(), new GetCalendarEventTool(), new() { ["eventId"] = "missing" });
        var json = Parse(admin);

        // Assert
        Assert.Equal("Test", json.GetProperty("title").GetString());
        Assert.Equal("2026-11-04T10:00", json.GetProperty("start").GetString());
        Assert.Equal("jane@example.com", json.GetProperty("attendees")[0].GetProperty("email").GetString());
        Assert.DoesNotContain("answer-token", admin.ToString(), StringComparison.Ordinal);
        Assert.Equal(GetCalendarEventTool.NotFound, john);
        Assert.Equal(GetCalendarEventTool.NotFound, missing);
    }

    [Fact]
    public async Task UpdateCalendarEvent_ChangesTheFieldsGiven_AndRefusesBeforeChangingAnything()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects", c =>
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write }));
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1), e =>
        {
            e.Status = CalendarEventStatus.Tentative;
            e.Location = "Room 1";
        });
        var jane = CalendarTestHarness.User("jane", _access);
        var id = created.Value.ItemId;

        // Act
        var confirm = await InvokeAsync(harness, jane, new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["status"] = "Confirmed" });
        var badTime = await InvokeAsync(harness, jane, new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["title"] = "Moved", ["start"] = "next tuesday" });
        var badVisibility = await InvokeAsync(harness, jane, new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["visibility"] = "Secret" });
        var notRepeating = await InvokeAsync(harness, jane, new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["occurrenceStart"] = "2026-11-04T15:00:00Z" });
        var unchanged = await FindEventAsync(harness, id);
        var updated = await InvokeAsync(harness, jane, new UpdateCalendarEventTool(), new()
        {
            ["eventId"] = id,
            ["title"] = "Planning",
            ["visibility"] = "Private",
            ["location"] = string.Empty,
            ["guestEmails"] = JsonSerializer.SerializeToElement(new[] { "guest@example.com" }),
        });
        var saved = await FindEventAsync(harness, id);
        var john = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["title"] = "Mine" });
        var adminConfirms = await InvokeAsync(harness, Admin(), new UpdateCalendarEventTool(), new() { ["eventId"] = id, ["status"] = "Confirmed" });

        // Assert
        Assert.Equal("Only someone who manages the calendar can confirm a tentative event.", confirm);
        Assert.Equal("Start is not an ISO 8601 time.", badTime);
        Assert.Equal("visibility must be Default, Public or Private.", badVisibility);
        Assert.StartsWith("occurrenceStart applies only to a repeating event.", notRepeating.ToString(), StringComparison.Ordinal);
        Assert.Equal("Test", unchanged.Title);
        Assert.Equal("Room 1", unchanged.Location);
        Assert.Equal("Event", Parse(updated).GetProperty("changed").GetString());
        Assert.Equal("Planning", saved.Title);
        Assert.Equal(CalendarEventVisibility.Private, saved.Visibility);
        Assert.Null(saved.Location);
        Assert.Equal("guest@example.com", Assert.Single(saved.Attendees).Email);
        Assert.Equal("The user can't change that event.", john);
        Assert.Equal("Confirmed", Parse(adminConfirms).GetProperty("calendarEvent").GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteCalendarEvent_NeedsWriteAccess()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects", c =>
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Read }));
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1));

        // Act
        var jane = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new DeleteCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId });
        var stillThere = await FindEventAsync(harness, created.Value.ItemId);
        var admin = await InvokeAsync(harness, Admin(), new DeleteCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId });

        // Assert
        Assert.Equal("The user can't change that event.", jane);
        Assert.NotNull(stillThere);
        Assert.True(Parse(admin).GetProperty("deleted").GetBoolean());
        Assert.Null(await FindEventAsync(harness, created.Value.ItemId));
    }

    [Fact]
    public async Task RespondToCalendarEvent_RecordsTheCallersOwnAnswer_AndOnlyForAnInvitedUser()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects");
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1), e =>
            e.Attendees.Add(new CalendarAttendee { UserId = "jane", Name = "jane", Role = CalendarAttendeeRole.Required }));

        // Act
        var badAnswer = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new RespondToCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId, ["response"] = "Maybe" });
        var john = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new RespondToCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId, ["response"] = "Accepted" });
        var jane = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new RespondToCalendarEventTool(), new() { ["eventId"] = created.Value.ItemId, ["response"] = "Accepted" });
        var saved = await FindEventAsync(harness, created.Value.ItemId);

        // Assert
        Assert.Equal("Use Accepted, Tentative or Declined.", badAnswer);
        Assert.StartsWith("Your answer could not be saved:", john.ToString(), StringComparison.Ordinal);
        Assert.Equal("Accepted", Parse(jane).GetProperty("response").GetString());
        Assert.Equal(CalendarAttendeeResponse.Accepted, Assert.Single(saved.Attendees).Response);
    }

    [Fact]
    public async Task Categories_FollowTheCategoriesPageRules()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var john = CalendarTestHarness.User("john", _access);

        // Act
        var own = Parse(await InvokeAsync(harness, john, new SaveCalendarCategoryTool(), new() { ["name"] = "Focus", ["color"] = "#FF0000" }));
        var johnShares = await InvokeAsync(harness, john, new SaveCalendarCategoryTool(), new() { ["name"] = "Everyone", ["shared"] = true });
        var badColor = await InvokeAsync(harness, john, new SaveCalendarCategoryTool(), new() { ["name"] = "Blue", ["color"] = "blue" });
        var shared = Parse(await InvokeAsync(harness, Admin(), new SaveCalendarCategoryTool(), new() { ["name"] = "Holidays", ["shared"] = true }));
        var recolored = Parse(await InvokeAsync(harness, Admin(), new SaveCalendarCategoryTool(), new() { ["categoryId"] = shared.GetProperty("id").GetString(), ["color"] = "#00ff00" }));
        var johnDeletesShared = await InvokeAsync(harness, john, new DeleteCalendarCategoryTool(), new() { ["categoryId"] = shared.GetProperty("id").GetString() });
        var list = Parse(await InvokeAsync(harness, john, new ListCalendarCategoriesTool(), new() { ["pageSize"] = 1 }));
        var johnDeletesOwn = await InvokeAsync(harness, john, new DeleteCalendarCategoryTool(), new() { ["categoryId"] = own.GetProperty("id").GetString() });
        var afterDelete = Parse(await InvokeAsync(harness, john, new ListCalendarCategoriesTool(), []));

        // Assert
        Assert.Equal("#ff0000", own.GetProperty("color").GetString());
        Assert.False(own.GetProperty("shared").GetBoolean());
        Assert.Equal("Only a calendar administrator can share a category with everyone.", johnShares);
        Assert.Equal("Choose a #rrggbb color, such as #1971c2.", badColor);
        Assert.True(recolored.GetProperty("shared").GetBoolean());
        Assert.Equal("#00ff00", recolored.GetProperty("color").GetString());
        Assert.Equal("Only a calendar administrator can delete a shared category.", johnDeletesShared);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.True(list.GetProperty("truncated").GetBoolean());
        Assert.True(Parse(johnDeletesOwn).GetProperty("deleted").GetBoolean());
        Assert.Equal("Holidays", Assert.Single(afterDelete.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetTeamAvailability_IsForTeamCalendarsOnly()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects");

        // Act
        var general = await InvokeAsync(harness, Admin(), new GetTeamAvailabilityTool(), new() { ["calendarId"] = calendar.ItemId });
        var missing = await InvokeAsync(harness, Admin(), new GetTeamAvailabilityTool(), new() { ["calendarId"] = "missing" });

        // Assert
        Assert.Equal("That calendar is not a Team calendar. Use findFreeTime for other calendars.", general);
        Assert.Equal(CalendarToolSupport.CalendarNotFound, missing);
    }

    [Fact]
    public async Task ResetCalendarFeedAddress_IsForManagers_AndNeverReturnsTheAddress()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company holidays", c =>
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write }));

        // Act
        var janeResets = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new ResetCalendarFeedAddressTool(), new() { ["calendarId"] = calendar.ItemId, ["action"] = "new" });
        var made = await InvokeAsync(harness, Admin(), new ResetCalendarFeedAddressTool(), new() { ["calendarId"] = calendar.ItemId, ["action"] = "new" });
        var token = (await FindCalendarAsync(harness, calendar.ItemId)).FeedToken;
        var removed = await InvokeAsync(harness, Admin(), new ResetCalendarFeedAddressTool(), new() { ["calendarId"] = calendar.ItemId, ["action"] = "remove" });

        // Assert
        Assert.Equal("Only someone who manages the calendar can change its subscription address.", janeResets);
        Assert.False(string.IsNullOrEmpty(token));
        Assert.DoesNotContain(token, made.ToString(), StringComparison.Ordinal);
        Assert.True(Parse(made).GetProperty("hasFeedAddress").GetBoolean());
        Assert.False(Parse(removed).GetProperty("hasFeedAddress").GetBoolean());
        Assert.Null((await FindCalendarAsync(harness, calendar.ItemId)).FeedToken);
    }

    private static ClaimsPrincipal Admin()
        => CalendarTestHarness.User("admin", _manageAll);

    private static JsonElement Parse(object result)
    {
        var text = Assert.IsType<string>(result);

        Assert.StartsWith("{", text, StringComparison.Ordinal);

        return JsonElement.Parse(text);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(services =>
        {
            services.AddScoped<CalendarEventEditor>();
            services.AddScoped<CalendarUserLookup>();
            services.AddSingleton<IUserSearchService, FakeUserSearchService>();
            services.AddSingleton<IRoleService, FakeRoleService>();
            services.AddCalendarProvider<FakeIcsProvider>();
            services.AddSingleton<TestHttpContextAccessor>();
            services.AddSingleton<IHttpContextAccessor>(provider => provider.GetRequiredService<TestHttpContextAccessor>());
        });
    }

    private static Task<object> InvokeAsync(CalendarTestHarness harness, ClaimsPrincipal user, AIFunction tool, Dictionary<string, object> arguments)
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            return await tool.InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);
        });
    }

    private static Task<Calendar> FindCalendarAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICalendarService>().FindByIdAsync(id).AsTask(), commit: false);
    }

    private static Task<CalendarEvent> FindEventAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICalendarEventManager>().FindByIdAsync(id).AsTask(), commit: false);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    private sealed class FakeUserSearchService : IUserSearchService
    {
        private static readonly string[] _users = ["admin", "john", "jane"];

        public Task<IReadOnlyList<UserSearchResult>> SearchAsync(UserSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSearchResult>>([]);

        public Task<IReadOnlyList<UserSearchResult>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSearchResult>>(userIds
                .Where(id => _users.Contains(id, StringComparer.Ordinal))
                .Select(id => new UserSearchResult { UserId = id, UserName = id, Email = id + "@example.com", IsEnabled = true })
                .ToList());
    }

    private sealed class FakeRoleService : IRoleService
    {
        public Task<IEnumerable<IRole>> GetRolesAsync()
            => Task.FromResult<IEnumerable<IRole>>([new FakeRole("Administrator"), new FakeRole("Sales"), new FakeRole("Anonymous")]);

        public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<Claim>>([]);

        public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync()
            => Task.FromResult<IEnumerable<string>>(["ADMINISTRATOR", "SALES", "ANONYMOUS"]);
    }

    private sealed record FakeRole(string RoleName) : IRole
    {
        public string RoleDescription => null;
    }

    private sealed class FakeIcsProvider : ICalendarProvider
    {
        public string Name => "ics";

        public LocalizationSource DisplayName { get; } = new("iCalendar address");

        public Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ICalendarEventData>>([]);
    }
}
