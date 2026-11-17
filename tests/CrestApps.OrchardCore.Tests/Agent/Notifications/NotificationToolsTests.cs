using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Notifications;
using CrestApps.OrchardCore.Tests.Agent.Security;

namespace CrestApps.OrchardCore.Tests.Agent.Notifications;

/// <summary>
/// The notification tools act on the notifications of the signed-in caller only: they list with the filter of the
/// Notifications admin page, and a notification that was sent to another user is never returned, marked or removed,
/// even when its ID is given.
/// </summary>
public sealed class NotificationToolsTests
{
    [Fact]
    public async Task ListNotifications_ReturnsTheCallersNotifications()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListNotificationsTool(), services));

        // Assert
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var first = items[0];

        Assert.Equal(3, result.GetProperty("total").GetInt32());
        Assert.Equal(["note-1", "note-2", "note-3"], items.Select(item => item.GetProperty("id").GetString()));
        Assert.Equal("Import failed", first.GetProperty("subject").GetString());
        Assert.Equal("The product import failed.", first.GetProperty("summary").GetString());
        Assert.Equal("Import", first.GetProperty("type").GetString());
        Assert.False(first.GetProperty("read").GetBoolean());
        Assert.True(items[2].GetProperty("read").GetBoolean());
        Assert.Equal("2026-11-16T08:00:00+00:00", first.GetProperty("createdAt").GetString());
        Assert.Equal(["Import", "Mention"], result.GetProperty("knownTypes").EnumerateArray().Select(type => type.GetString()));
    }

    [Fact]
    public async Task ListNotifications_TurnsTheArgumentsIntoTheFilterOfTheAdminPage()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new ListNotificationsTool(), services, new()
        {
            ["status"] = "unread",
            ["type"] = "Mention",
            ["text"] = "failed import",
            ["sort"] = "oldest",
        });

        // Assert: the parser of the admin page took each argument as the filter it names.
        var filter = harness.Notifications.NormalizedFilter;

        Assert.Contains("status:Unread", filter, StringComparison.Ordinal);
        Assert.Contains("type:Mention", filter, StringComparison.Ordinal);
        Assert.Contains("sort:Oldest", filter, StringComparison.Ordinal);
        Assert.Contains("failed import", filter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNotifications_NeverReturnsTheNotificationOfAnotherUser()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        // The query of Orchard Core keeps to the user of the request. The tool does not rely on that alone.
        harness.Notifications.QueryReturnsEveryUser = true;

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new ListNotificationsTool(), services);

        // Assert
        var items = AgentToolTestContext.ReadJson(text).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetString() == NotificationToolDoubles.OtherUserNotificationId);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("status", "archived", "status must be read or unread")]
    [InlineData("type", "Mention sort:Oldest", "type must be the technical name")]
    [InlineData("text", "say \"hi\"", "text cannot contain a double quote")]
    [InlineData("sort", "random", "sort must be newest or oldest")]
    public async Task ListNotifications_WithAnArgumentTheFilterCannotTake_ListsNothing(string argument, string value, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListNotificationsTool(), services, new()
        {
            [argument] = value,
        });

        // Assert
        Assert.StartsWith("Nothing was listed:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Null(harness.Notifications.NormalizedFilter);
    }

    [Fact]
    public async Task MarkNotifications_WithIds_MarksTheCallersNotificationsAndLeavesTheOneOfAnotherUser()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new MarkNotificationsTool(), services, new()
        {
            ["action"] = "read",
            ["notificationIds"] = new[] { "note-1", "note-3", NotificationToolDoubles.OtherUserNotificationId, "note-missing" },
        }));

        // Assert
        Assert.Equal(["note-1"], result.GetProperty("changed").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(["note-3"], result.GetProperty("alreadyInThatState").EnumerateArray().Select(id => id.GetString()));

        // The notification of the other user is answered like one that does not exist.
        Assert.Equal([NotificationToolDoubles.OtherUserNotificationId, "note-missing"], result.GetProperty("notFound").EnumerateArray().Select(id => id.GetString()));
        Assert.True(harness.Notifications.IsRead("note-1"));
        Assert.False(harness.Notifications.IsRead(NotificationToolDoubles.OtherUserNotificationId));
    }

    [Fact]
    public async Task MarkNotifications_AsUnread_MarksTheGivenNotification()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new MarkNotificationsTool(), services, new()
        {
            ["action"] = "unread",
            ["notificationIds"] = new[] { "note-3" },
        });

        // Assert
        Assert.False(harness.Notifications.IsRead("note-3"));
    }

    [Fact]
    public async Task MarkNotifications_WithAll_MarksEveryUnreadNotificationOfTheCallerOnly()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new MarkNotificationsTool(), services, new()
        {
            ["action"] = "read",
            ["all"] = true,
        }));

        // Assert
        Assert.Equal(2, result.GetProperty("changedCount").GetInt32());
        Assert.True(harness.Notifications.IsRead("note-1"));
        Assert.True(harness.Notifications.IsRead("note-2"));
        Assert.False(harness.Notifications.IsRead(NotificationToolDoubles.OtherUserNotificationId));
    }

    [Theory]
    [InlineData("""{ "action": "unread", "all": true }""", "all works with the action read only")]
    [InlineData("""{ "action": "read" }""", "give notificationIds, or set all to true, not both")]
    [InlineData("""{ "action": "read", "all": true, "notificationIds": [ "note-1" ] }""", "give notificationIds, or set all to true, not both")]
    [InlineData("""{ "action": "archive", "all": true }""", "action must be read or unread")]
    [InlineData("""{ "action": "read", "notificationIds": "note-1" }""", "notificationIds must be a list")]
    public async Task MarkNotifications_WithArgumentsThatDoNotFit_ChangesNothing(string json, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MarkNotificationsTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was changed:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteNotifications_RemovesTheCallersNotificationsAndLeavesTheOneOfAnotherUser()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteNotificationsTool(), services, new()
        {
            ["notificationIds"] = new[] { "note-2", NotificationToolDoubles.OtherUserNotificationId },
        }));

        // Assert
        Assert.Equal(["note-2"], result.GetProperty("deleted").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal([NotificationToolDoubles.OtherUserNotificationId], result.GetProperty("notFound").EnumerateArray().Select(id => id.GetString()));
        Assert.DoesNotContain(harness.Notifications.Notifications, notification => notification.NotificationId == "note-2");
        Assert.Contains(harness.Notifications.Notifications, notification => notification.NotificationId == NotificationToolDoubles.OtherUserNotificationId);
    }

    [Fact]
    public async Task DeleteNotifications_WithoutIds_RemovesNothing()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteNotificationsTool(), services, new()
        {
            ["notificationIds"] = Array.Empty<string>(),
        });

        // Assert
        Assert.StartsWith("Nothing was removed: give notificationIds", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }
}
