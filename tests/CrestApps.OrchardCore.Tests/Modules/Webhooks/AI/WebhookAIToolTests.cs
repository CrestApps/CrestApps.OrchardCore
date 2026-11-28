using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.AI;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using Microsoft.Extensions.AI;
using AgentToolTestContext = CrestApps.OrchardCore.Tests.Agent.AgentToolTestContext;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks.AI;

/// <summary>
/// The webhook tools need <c>ManageCommerceWebhooks</c>, check what the webhook editor checks, never take or return a
/// signing secret, and ping and resend through the real dispatcher.
/// </summary>
public sealed class WebhookAIToolTests
{
    private readonly WebhookToolHarness _harness = new();

    public static TheoryData<string> ToolNames => new()
    {
        ListWebhooksTool.TheName,
        ListWebhookEventsTool.TheName,
        SaveWebhookTool.TheName,
        RotateWebhookSecretTool.TheName,
        DeleteWebhookTool.TheName,
        ListWebhookDeliveriesTool.TheName,
        SendWebhookDeliveryTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var protectedSecret = webhook.ProtectedSecret;
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        var services = _harness.Build(signedIn: false);

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments());

        // Assert
        Assert.Equal(WebhooksAIToolBase.NotAllowedMessage, result);
        AssertUnchanged(protectedSecret);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageCommerceWebhooks(string toolName)
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var protectedSecret = webhook.ProtectedSecret;
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        var services = _harness.Build(granted: false);

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments());

        // Assert
        Assert.Equal(WebhooksAIToolBase.NotAllowedMessage, result);
        Assert.Equal("ManageCommerceWebhooks", Assert.Single(_harness.Authorization.Checks).Permission);
        AssertUnchanged(protectedSecret);
    }

    [Fact]
    public async Task ListWebhooks_ReturnsTheWebhookWithoutItsSecret()
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        _harness.AddDelivery("d2", "w1", WebhookDeliveryStatus.Delivered);
        var secret = _harness.Dispatcher.GetSecret(webhook);
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(ListWebhooksTool.TheName, services, []);
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal("https://erp.example.com/hooks", item.GetProperty("url").GetString());
        Assert.Equal("order.paid", Assert.Single(item.GetProperty("events").EnumerateArray()).GetString());
        Assert.True(item.GetProperty("hasSecret").GetBoolean());
        Assert.Equal(1, item.GetProperty("failedDeliveries").GetInt32());
        Assert.Equal("/Admin/webhooks/Edit/w1", item.GetProperty("editUrl").GetString());
        AssertNoSecret(result, secret, webhook.ProtectedSecret);
    }

    [Fact]
    public async Task ListWebhooks_WithAnUnknownId_SaysSo()
    {
        // Arrange
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(ListWebhooksTool.TheName, services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no webhook with the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListWebhookEvents_ListsTheEventsByGroup()
    {
        // Arrange
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListWebhookEventsTool.TheName, services, []));

        // Assert
        var group = Assert.Single(json.GetProperty("groups").EnumerateArray());

        Assert.Equal("Orders", group.GetProperty("group").GetString());
        Assert.Equal(new[] { "order.created", "order.paid" }, group.GetProperty("events").EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task SaveWebhook_CreatesAWebhookWithASecretItNeverReturns_AndReportsUnknownEvents()
    {
        // Arrange
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(SaveWebhookTool.TheName, services, new()
        {
            ["name"] = "ERP",
            ["url"] = "https://erp.example.com/hooks",
            ["events"] = new[] { "order.paid", "order.refunded" },
        });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var webhook = Assert.Single(_harness.Subscriptions.Values);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(new[] { "order.paid" }, webhook.Events);
        Assert.True(webhook.IsEnabled);
        Assert.False(string.IsNullOrEmpty(webhook.ProtectedSecret));
        Assert.Equal("order.refunded", Assert.Single(json.GetProperty("ignoredEvents").EnumerateArray()).GetString());
        Assert.Equal($"/Admin/webhooks/Edit/{webhook.ItemId}", json.GetProperty("webhook").GetProperty("editUrl").GetString());
        AssertNoSecret(result, _harness.Dispatcher.GetSecret(webhook), webhook.ProtectedSecret);
    }

    [Theory]
    [InlineData("http://erp.example.com/hooks", "order.paid", "https://")]
    [InlineData("https://erp.example.com/hooks", "order.refunded", "Choose the events")]
    [InlineData("not a url", "order.paid", "https://")]
    public async Task SaveWebhook_RefusesWhatTheEditorRefuses(string url, string eventName, string expected)
    {
        // Arrange
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveWebhookTool.TheName, services, new()
        {
            ["name"] = "ERP",
            ["url"] = url,
            ["events"] = new[] { eventName },
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetString().Contains(expected, StringComparison.Ordinal));
        Assert.Empty(_harness.Subscriptions);
    }

    [Fact]
    public async Task SaveWebhook_ById_ChangesOnlyWhatIsGiven_AndKeepsTheSecret()
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var protectedSecret = webhook.ProtectedSecret;
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveWebhookTool.TheName, services, new() { ["id"] = "w1", ["enabled"] = false }));

        // Assert
        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.False(webhook.IsEnabled);
        Assert.Equal("https://erp.example.com/hooks", webhook.Url);
        Assert.Equal(new[] { "order.paid" }, webhook.Events);
        Assert.Equal(protectedSecret, webhook.ProtectedSecret);
    }

    [Fact]
    public async Task SaveWebhook_RefusesASecretArgument()
    {
        // Arrange
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(SaveWebhookTool.TheName, services, new()
        {
            ["name"] = "ERP",
            ["url"] = "https://erp.example.com/hooks",
            ["allEvents"] = true,
            ["Secret"] = "whsec_given",
        });

        // Assert
        Assert.StartsWith("Refused:", result, StringComparison.Ordinal);
        Assert.Empty(_harness.Subscriptions);
    }

    [Fact]
    public async Task RotateWebhookSecret_GivesANewSecretAndReturnsNeither()
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var oldProtected = webhook.ProtectedSecret;
        var oldSecret = _harness.Dispatcher.GetSecret(webhook);
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(RotateWebhookSecretTool.TheName, services, new() { ["id"] = "w1" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("rotated").GetBoolean());
        Assert.NotEqual(oldProtected, webhook.ProtectedSecret);
        AssertNoSecret(result, oldSecret, oldProtected);
        AssertNoSecret(result, _harness.Dispatcher.GetSecret(webhook), webhook.ProtectedSecret);
    }

    [Fact]
    public async Task RotateWebhookSecret_WithAnUnknownId_ChangesNothing()
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var protectedSecret = webhook.ProtectedSecret;
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(RotateWebhookSecretTool.TheName, services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no webhook with the id 'missing'", result, StringComparison.Ordinal);
        Assert.Equal(protectedSecret, webhook.ProtectedSecret);
    }

    [Fact]
    public async Task DeleteWebhook_DeletesItAndItsDeliveries()
    {
        // Arrange
        _harness.AddWebhook();
        _harness.AddWebhook("w2");
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        _harness.AddDelivery("d2", "w2", WebhookDeliveryStatus.Failed);
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteWebhookTool.TheName, services, new() { ["id"] = "w1" }));

        // Assert
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal(new[] { "w2" }, _harness.Subscriptions.Keys);
        Assert.Equal(new[] { "d2" }, _harness.Deliveries.Keys);
    }

    [Fact]
    public async Task DeleteWebhook_WithAnUnknownId_DeletesNothing()
    {
        // Arrange
        _harness.AddWebhook();
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(DeleteWebhookTool.TheName, services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no webhook with the id 'missing'", result, StringComparison.Ordinal);
        Assert.Single(_harness.Subscriptions);
    }

    [Fact]
    public async Task SendWebhookDelivery_WithPing_PostsASignedPingAndReturnsTheAnswer()
    {
        // Arrange
        var webhook = _harness.AddWebhook();
        var secret = _harness.Dispatcher.GetSecret(webhook);
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1", ["ping"] = true });
        var json = AgentToolTestContext.ReadJson(result).GetProperty("result");

        // Assert
        var request = Assert.Single(_harness.Sent);
        var delivery = Assert.Single(_harness.Deliveries.Values);

        Assert.Equal("https://erp.example.com/hooks", request.Url);
        Assert.Equal(WebhookEvents.Ping, request.EventName);
        Assert.Equal(secret, request.Secret);
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.True(json.GetProperty("delivered").GetBoolean());
        Assert.Equal(200, json.GetProperty("statusCode").GetInt32());
        AssertNoSecret(result, secret, webhook.ProtectedSecret);
    }

    [Fact]
    public async Task SendWebhookDelivery_WithPing_WhenTheReceiverFails_ReturnsTheError()
    {
        // Arrange
        _harness.AddWebhook();
        _harness.SendResult = new() { Succeeded = false, StatusCode = 401, Error = "Bad signature" };
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1", ["ping"] = true })).GetProperty("result");

        // Assert
        Assert.False(json.GetProperty("delivered").GetBoolean());
        Assert.Equal(401, json.GetProperty("statusCode").GetInt32());
        Assert.Equal("Bad signature", json.GetProperty("error").GetString());
        Assert.True(json.GetProperty("triedAgainLater").GetBoolean());
    }

    [Fact]
    public async Task ListWebhookDeliveries_FiltersByStatus_WithoutPayloads()
    {
        // Arrange
        _harness.AddWebhook();
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        _harness.AddDelivery("d2", "w1", WebhookDeliveryStatus.Delivered);
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(ListWebhookDeliveriesTool.TheName, services, new() { ["webhookId"] = "w1", ["status"] = "failed" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("d1", item.GetProperty("id").GetString());
        Assert.Equal("Server error", item.GetProperty("lastError").GetString());
        Assert.DoesNotContain("secretPayload", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListWebhookDeliveries_RefusesAnUnknownStatus()
    {
        // Arrange
        _harness.AddWebhook();
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(ListWebhookDeliveriesTool.TheName, services, new() { ["webhookId"] = "w1", ["status"] = "1" });

        // Assert
        Assert.StartsWith("Give status as", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendWebhookDelivery_WithADeliveryId_SendsTheDeliveryAgain()
    {
        // Arrange
        _harness.AddWebhook();
        var delivery = _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        var services = _harness.Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1", ["deliveryId"] = "d1" })).GetProperty("result");

        // Assert
        Assert.Single(_harness.Sent);
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.True(json.GetProperty("delivered").GetBoolean());
    }

    [Fact]
    public async Task SendWebhookDelivery_RefusesADeliveryOfAnotherWebhook()
    {
        // Arrange
        _harness.AddWebhook();
        _harness.AddWebhook("w2");
        var delivery = _harness.AddDelivery("d2", "w2", WebhookDeliveryStatus.Failed);
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1", ["deliveryId"] = "d2" });

        // Assert
        Assert.Contains("has no delivery with the id 'd2'", result, StringComparison.Ordinal);
        Assert.Empty(_harness.Sent);
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
    }

    [Fact]
    public async Task SendWebhookDelivery_WithBothPingAndADeliveryId_SendsNothing()
    {
        // Arrange
        _harness.AddWebhook();
        _harness.AddDelivery("d1", "w1", WebhookDeliveryStatus.Failed);
        var services = _harness.Build();

        // Act
        var both = await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1", ["deliveryId"] = "d1", ["ping"] = true });
        var neither = await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "w1" });

        // Assert
        Assert.StartsWith("Nothing was sent: give either ping true", both, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was sent: give either ping true", neither, StringComparison.Ordinal);
        Assert.Empty(_harness.Sent);
    }

    [Fact]
    public async Task SendWebhookDelivery_WithPing_SaysWhenTheWebhookIsUnknown()
    {
        // Arrange
        _harness.AddWebhook();
        var services = _harness.Build();

        // Act
        var result = await InvokeAsync(SendWebhookDeliveryTool.TheName, services, new() { ["webhookId"] = "missing", ["ping"] = true });

        // Assert
        Assert.Contains("no webhook with the id 'missing'", result, StringComparison.Ordinal);
        Assert.Empty(_harness.Sent);
    }

    private void AssertUnchanged(string protectedSecret)
    {
        var webhook = Assert.Single(_harness.Subscriptions.Values);

        Assert.Equal(protectedSecret, webhook.ProtectedSecret);
        Assert.True(webhook.IsEnabled);
        Assert.Equal(WebhookDeliveryStatus.Failed, Assert.Single(_harness.Deliveries.Values).Status);
        Assert.Empty(_harness.Sent);
    }

    private static void AssertNoSecret(string result, string secret, string protectedSecret)
    {
        Assert.False(string.IsNullOrEmpty(secret));
        Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedSecret, result, StringComparison.Ordinal);
        Assert.DoesNotContain("whsec_", result, StringComparison.Ordinal);
    }

    // Arguments that reach every tool's permission check and would change something if it were passed.
    private static Dictionary<string, object> AllArguments()
        => new()
        {
            ["id"] = "w1",
            ["webhookId"] = "w1",
            ["deliveryId"] = "d1",
            ["name"] = "Changed",
            ["url"] = "https://other.example.com/hooks",
            ["enabled"] = false,
            ["allEvents"] = true,
        };

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, arguments);

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListWebhooksTool.TheName => new ListWebhooksTool(),
            ListWebhookEventsTool.TheName => new ListWebhookEventsTool(),
            SaveWebhookTool.TheName => new SaveWebhookTool(),
            RotateWebhookSecretTool.TheName => new RotateWebhookSecretTool(),
            DeleteWebhookTool.TheName => new DeleteWebhookTool(),
            ListWebhookDeliveriesTool.TheName => new ListWebhookDeliveriesTool(),
            SendWebhookDeliveryTool.TheName => new SendWebhookDeliveryTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
