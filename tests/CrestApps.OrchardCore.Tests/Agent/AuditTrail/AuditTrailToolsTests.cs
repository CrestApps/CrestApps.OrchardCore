using CrestApps.OrchardCore.AI.Agent.AuditTrail;
using CrestApps.OrchardCore.Tests.Agent.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.AuditTrail;

/// <summary>
/// The audit trail tools list with the filter of the Audit Trail admin page and read one event through the audit
/// trail manager. The details of an event are built from an explicit list of fields, and the details of an event
/// about a user need the permission the Users module checks for them.
/// </summary>
public sealed class AuditTrailToolsTests
{
    [Fact]
    public async Task ListAuditTrailEvents_TurnsTheArgumentsIntoTheFilterOfTheAdminPage()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new ListAuditTrailEventsTool(), services, new()
        {
            ["category"] = "user",
            ["eventName"] = "loginfailed",
            ["userName"] = "jane doe",
            ["userId"] = "user-jane",
            ["correlationId"] = "item-1",
            ["from"] = "2026-11-10",
            ["to"] = "2026-11-12T18:30",
            ["sort"] = "oldest",
            ["page"] = 2,
            ["pageSize"] = 3,
        });

        // Assert: the filter engine of the admin page gave each filter its value, whole.
        var values = harness.AuditTrail.FilterValues;

        Assert.Equal("User", values["category"]);
        Assert.Equal("LogInFailed", values["event"]);
        Assert.Equal("user-jane", values["userid"]);
        Assert.Equal("item-1", values["id"]);
        Assert.Equal("2026-11-10T00:00:00.000Z..2026-11-12T18:30:00.000Z", values["date"]);
        Assert.Equal("time-asc", values["sort"]);
        Assert.Equal("jane doe", values["username"]);
        Assert.Equal(7, values.Count);
        Assert.Equal((2, 3), harness.AuditTrail.Paging);
    }

    [Theory]
    [InlineData("from", "2026-11-10", ">=2026-11-10T00:00:00.000Z")]
    [InlineData("from", "2026-11-10T08:15", ">=2026-11-10T08:15:00.000Z")]
    [InlineData("to", "2026-11-10", "<=2026-11-11T00:00:00.000Z")]
    [InlineData("to", "2026-11-10T08:15", "<=2026-11-10T08:15:00.000Z")]
    public async Task ListAuditTrailEvents_WithOneEndOfADateRange_FiltersFromOrUpToIt(string argument, string value, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new ListAuditTrailEventsTool(), services, new()
        {
            [argument] = value,
        });

        // Assert: a date without a time takes in the whole day.
        Assert.Equal(expected, harness.AuditTrail.FilterValues["date"]);
        Assert.Single(harness.AuditTrail.FilterValues);
    }

    [Fact]
    public async Task ListAuditTrailEvents_ReturnsWhatHappenedWhoDidItAndWhen_WithoutTheAddressOrTheData()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new ListAuditTrailEventsTool(), services);

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var login = items[0];

        Assert.Equal(5, result.GetProperty("total").GetInt32());
        Assert.Empty(harness.AuditTrail.FilterValues);
        Assert.Equal("event-login", login.GetProperty("eventId").GetString());
        Assert.Equal("User", login.GetProperty("category").GetString());
        Assert.Equal("LoggedIn", login.GetProperty("event").GetString());
        Assert.Equal("LoggedIn (title)", login.GetProperty("title").GetString());
        Assert.Equal("jane", login.GetProperty("userName").GetString());
        Assert.Equal("2026-11-16T09:30:00+00:00", login.GetProperty("createdAt").GetString());
        Assert.Equal("UTC", result.GetProperty("timeZone").GetString());
        Assert.All(items, item => Assert.Subset(
            new HashSet<string> { "eventId", "category", "event", "title", "correlationId", "userId", "userName", "createdAt" },
            item.EnumerateObject().Select(property => property.Name).ToHashSet()));
        Assert.DoesNotContain("203.0.113.7", text, StringComparison.Ordinal);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("category", "Orders", "there is no category named 'Orders'. The categories are: User, Content.")]
    [InlineData("eventName", "Exploded", "there is no event named 'Exploded'.")]
    [InlineData("from", "last week", "from must be a local date")]
    [InlineData("to", "11/12/2026", "to must be a local date")]
    [InlineData("userId", "user jane", "userId must be an id")]
    [InlineData("correlationId", "item-1 sort:time-asc", "correlationId must be an id")]
    [InlineData("userName", "ja\"ne", "userName cannot contain a double quote")]
    [InlineData("sort", "random", "sort must be newest or oldest")]
    public async Task ListAuditTrailEvents_WithAnArgumentTheFilterCannotTake_ListsNothing(string argument, string value, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAuditTrailEventsTool(), services, new()
        {
            [argument] = value,
        });

        // Assert
        Assert.StartsWith("Nothing was listed:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Null(harness.AuditTrail.FilterText);
    }

    [Fact]
    public async Task ListAuditTrailEvents_WithAnEventOfAnotherCategory_IsRefused()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAuditTrailEventsTool(), services, new()
        {
            ["category"] = "Content",
            ["eventName"] = "LoggedIn",
        });

        // Assert
        Assert.Equal("Nothing was listed: there is no event named 'LoggedIn'. The events are: Content: Published, Saved.", result);
    }

    [Fact]
    public async Task GetAuditTrailEvent_AboutAnotherUser_ReturnsTheAllowListedFieldsOfTheSnapshot()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-user-updated",
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var user = result.GetProperty("user");
        var snapshot = user.GetProperty("snapshot");
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal("event-user-updated", result.GetProperty("auditTrailEvent").GetProperty("eventId").GetString());
        Assert.Equal("203.0.113.7", result.GetProperty("clientIpAddress").GetString());
        Assert.Equal("user-jane", user.GetProperty("userId").GetString());
        Assert.Equal("jane", user.GetProperty("userName").GetString());
        Assert.Equal(["Email", "IsEnabled", "RoleNames", "UserId", "UserName"], snapshot.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("j***@contoso.com", snapshot.GetProperty("Email").GetString());
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);

        // The page permission, then the permission of the Users module for the events about any user.
        Assert.Equal(["ViewAuditTrail", "ViewUserAuditTrailEvents"], checks.Select(check => check.Permission));
        Assert.All(checks, check => Assert.Null(check.Resource));
    }

    [Fact]
    public async Task GetAuditTrailEvent_AboutAnotherUser_WithoutThePermissionForUserEvents_ReturnsTheHeaderOnly()
    {
        // Arrange
        var harness = new SecurityToolHarness
        {
            Authorize = (permission, _) => permission.Name == "ViewAuditTrail",
        };

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-user-updated",
        }));

        // Assert
        Assert.Equal("Updated", result.GetProperty("auditTrailEvent").GetProperty("event").GetString());
        Assert.False(result.TryGetProperty("user", out _));
        Assert.Contains("you are not allowed to view audit trail events about users", result.GetProperty("detailsNote").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuditTrailEvent_AboutTheCallersOwnAccount_NeedsThePermissionForOwnEventsOnly()
    {
        // Arrange
        var harness = new SecurityToolHarness
        {
            Authorize = (permission, _) => permission.Name is "ViewAuditTrail" or "ViewOwnUserAuditTrailEvents",
        };

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-own",
        }));

        // Assert
        Assert.Equal(AgentToolTestContext.UserId, result.GetProperty("user").GetProperty("userId").GetString());
        Assert.Equal(["ViewAuditTrail", "ViewOwnUserAuditTrailEvents"], services.GetRequiredService<FakeAuthorizationService>().Checks.Select(check => check.Permission));
    }

    [Fact]
    public async Task GetAuditTrailEvent_AboutContent_ReturnsTheVersionItRecordedWithoutTheContent()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-content",
        });

        // Assert
        var content = AgentToolTestContext.ReadJson(text).GetProperty("content");

        Assert.Equal("item-1", content.GetProperty("contentItemId").GetString());
        Assert.Equal("version-3", content.GetProperty("contentItemVersionId").GetString());
        Assert.Equal("Article", content.GetProperty("contentType").GetString());
        Assert.Equal("About us", content.GetProperty("displayText").GetString());
        Assert.Equal(3, content.GetProperty("versionNumber").GetInt32());
        Assert.True(content.GetProperty("published").GetBoolean());
        Assert.Equal("Fixed a typo.", content.GetProperty("comment").GetString());
        Assert.Equal("2026-11-10T10:00:00+00:00", content.GetProperty("modifiedAt").GetString());
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);

        // A content event has no permission of its own on the admin page.
        Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Fact]
    public async Task GetAuditTrailEvent_OfATypeTheToolDoesNotKnow_ReturnsTheHeaderOnly()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-custom",
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);

        Assert.Equal("InvoiceSent", result.GetProperty("auditTrailEvent").GetProperty("event").GetString());
        Assert.False(result.TryGetProperty("user", out _));
        Assert.False(result.TryGetProperty("content", out _));
        Assert.Contains("details this tool does not know", result.GetProperty("detailsNote").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAuditTrailEvent_ForAnIdThatMatchesNoEvent_SaysSo()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetAuditTrailEventTool(), services, new()
        {
            ["eventId"] = "event-missing",
        });

        // Assert
        Assert.Equal("Nothing was found: there is no audit trail event with the id 'event-missing'. Use listAuditTrailEvents to find it.", result);
    }
}
