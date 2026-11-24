using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Fulfillment.AI;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Workforce;
using FulfillmentAIToolsStartup = CrestApps.OrchardCore.Fulfillment.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Fulfillment.AI;

/// <summary>
/// <c>sendOrderToFulfillmentPartners</c> refuses a caller who is not signed in or lacks Ship orders, queues a paid order
/// for the background sender instead of calling the partner, refuses an order that is not paid (also for one request),
/// never queues again what a partner has, queues a request the partner may already have only when the user checked with
/// the partner, leaves an order another worker is sending alone, and asks for approval.
/// </summary>
public sealed class SendOrderToFulfillmentPartnersToolTests : IAsyncLifetime
{
    private const string Partner = "Printify";

    private readonly FulfillmentTestHarness _harness = new();
    private FulfillmentTestHarness.FakeProvider _provider;

    public async ValueTask InitializeAsync()
    {
        await _harness.InitializeAsync();
        _provider = _harness.AddProvider(Partner, "l1");
    }

    public ValueTask DisposeAsync()
        => _harness.DisposeAsync();

    [Fact]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn()
    {
        // Act
        var (result, authorization) = await FulfillmentToolServices.InvokeAsync(_harness, new SendOrderToFulfillmentPartnersTool(), new() { ["orderId"] = _harness.Order.ItemId }, FulfillmentToolServices.Anonymous());

        // Assert
        Assert.Equal(FulfillmentAIToolBase.NotAllowedMessage, result);
        Assert.Empty(authorization.Checks);
        Assert.Empty(await _harness.LoadRequestsAsync());
        Assert.Equal(0, _provider.Calls);
    }

    [Fact]
    public async Task Tool_RefusesACallerWithoutShipOrders()
    {
        // Act
        var (result, authorization) = await FulfillmentToolServices.InvokeAsync(
            _harness,
            new SendOrderToFulfillmentPartnersTool(),
            new() { ["orderId"] = _harness.Order.ItemId },
            authorize: (permission, _) => permission.Name != ShippingPermissions.FulfillOrders.Name);

        // Assert
        Assert.Equal(FulfillmentAIToolBase.NotAllowedMessage, result);
        Assert.Equal(ShippingPermissions.FulfillOrders.Name, Assert.Single(authorization.Checks).Permission);
        Assert.Empty(await _harness.LoadRequestsAsync());
    }

    [Fact]
    public async Task Order_IsQueued_AndTheBackgroundTaskSendsIt()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await SendAsync(new() { ["orderId"] = _harness.Order.ItemId }));
        var callsBeforeTheTask = _provider.Calls;
        var waiting = Assert.Single(await _harness.LoadRequestsAsync());

        await _harness.ProcessDueAsync();

        // Assert
        var queued = Assert.Single(json.GetProperty("requests").EnumerateArray());

        Assert.True(json.GetProperty("queued").GetBoolean());
        Assert.Contains("listFulfillmentRequests", json.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Equal("pending", queued.GetProperty("status").GetString());
        Assert.Equal(Partner, queued.GetProperty("partner").GetString());
        Assert.Equal(FulfillmentAIToolBase.UntrustedNote, json.GetProperty("note").GetString());
        Assert.Equal(0, callsBeforeTheTask);
        Assert.Equal(FulfillmentRequestStatus.Pending, waiting.Status);
        Assert.Equal(1, _provider.Calls);
        Assert.Equal(FulfillmentRequestStatus.Sent, Assert.Single(await _harness.LoadRequestsAsync()).Status);
    }

    [Fact]
    public async Task Order_ThatIsNotPaid_IsRefused()
    {
        // Arrange
        _harness.Order.Status = OrderStatus.AwaitingPayment;

        // Act
        var result = await SendAsync(new() { ["orderNumber"] = _harness.Order.OrderNumber });

        // Assert
        Assert.Equal($"Nothing was queued: only a paid order is sent to fulfillment partners, and order {_harness.Order.OrderNumber} is awaitingPayment.", result);
        Assert.Empty(await _harness.LoadRequestsAsync());
    }

    [Fact]
    public async Task Request_OfAnOrderThatIsNotPaid_IsRefused()
    {
        // Arrange
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.Refused("The address is not valid."));
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);
        var request = Assert.Single(await _harness.LoadRequestsAsync());
        _harness.Order.Status = OrderStatus.Canceled;

        // Act
        var result = await SendAsync(new() { ["requestId"] = request.ItemId });

        // Assert
        Assert.StartsWith("Nothing was queued: only a paid order is sent to fulfillment partners", result);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, Assert.Single(await _harness.LoadRequestsAsync()).Status);
    }

    [Fact]
    public async Task Order_ThePartnerHas_IsNotQueuedAgain()
    {
        // Arrange
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);
        var request = Assert.Single(await _harness.LoadRequestsAsync());

        // Act
        var byOrder = AgentToolTestContext.ReadJson(await SendAsync(new() { ["orderId"] = _harness.Order.ItemId }));
        var byRequest = AgentToolTestContext.ReadJson(await SendAsync(new() { ["requestId"] = request.ItemId }));

        await _harness.ProcessDueAsync();

        // Assert
        Assert.False(byOrder.GetProperty("queued").GetBoolean());
        Assert.False(byRequest.GetProperty("queued").GetBoolean());
        Assert.StartsWith("Nothing was queued", byOrder.GetProperty("summary").GetString());
        Assert.Equal(1, _provider.Calls);
        Assert.Equal(FulfillmentRequestStatus.Sent, Assert.Single(await _harness.LoadRequestsAsync()).Status);
    }

    [Fact]
    public async Task Request_ThePartnerMayHave_IsQueuedOnlyAfterTheUserCheckedWithThePartner()
    {
        // Arrange
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.NeedsAttention("No answer."));
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);
        var unclear = Assert.Single(await _harness.LoadRequestsAsync());
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.Sent("partner-2", "Accepted."));

        // Act
        var refusedByOrder = await SendAsync(new() { ["orderId"] = _harness.Order.ItemId });
        var refusedByRequest = await SendAsync(new() { ["requestId"] = unclear.ItemId });
        var stillUnclear = Assert.Single(await _harness.LoadRequestsAsync());
        var queued = AgentToolTestContext.ReadJson(await SendAsync(new() { ["requestId"] = unclear.ItemId, ["checkedWithPartner"] = true }));
        var waiting = Assert.Single(await _harness.LoadRequestsAsync());

        await _harness.ProcessDueAsync();

        // Assert
        Assert.Equal(FulfillmentReasons.Unclear, unclear.Reason);
        Assert.StartsWith($"Nothing was queued: {Partner} may already have order", refusedByOrder);
        Assert.StartsWith($"Nothing was queued: {Partner} may already have order", refusedByRequest);
        Assert.Equal(FulfillmentReasons.Unclear, stillUnclear.Reason);
        Assert.True(queued.GetProperty("queued").GetBoolean());
        Assert.Equal(FulfillmentRequestStatus.Pending, waiting.Status);
        Assert.Equal(unclear.ShipmentId, waiting.ShipmentId);
        Assert.Equal(2, _provider.Calls);
        Assert.Equal(FulfillmentRequestStatus.Sent, Assert.Single(await _harness.LoadRequestsAsync()).Status);
        Assert.Single(await _harness.LoadShipmentsAsync());
    }

    [Fact]
    public async Task Order_AnotherWorkerIsSending_IsLeftAlone()
    {
        // Arrange
        _harness.LockAvailable = false;

        // Act
        var result = await SendAsync(new() { ["orderId"] = _harness.Order.ItemId });

        // Assert
        Assert.StartsWith("Nothing was queued: someone else, or the background task, is sending this order", result);
        Assert.Empty(await _harness.LoadRequestsAsync());
    }

    [Fact]
    public async Task Request_ThatDoesNotExist_OrNoTarget_SaysSo()
    {
        // Act
        var missing = await SendAsync(new() { ["requestId"] = "missing" });
        var none = await SendAsync([]);
        var both = await SendAsync(new() { ["orderId"] = _harness.Order.ItemId, ["requestId"] = "missing" });

        // Assert
        Assert.Equal("Nothing was queued: no fulfillment request has the id 'missing'. Use listFulfillmentRequests to find it.", missing);
        Assert.StartsWith("Nothing was queued: give the order's id or number, or a request id.", none);
        Assert.Equal("Nothing was queued: give either an order or a request id, not both.", both);
        Assert.Empty(await _harness.LoadRequestsAsync());
    }

    [Fact]
    public void Tool_IsRegisteredWithItsTexts_AndAsksForApproval()
    {
        // Act
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(FulfillmentAIToolsStartup));

        // Assert
        Assert.True(tools.TryGetValue(SendOrderToFulfillmentPartnersTool.TheName, out var tool));
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Fulfillment Partners", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.True(tool.RequiresApproval);
        Assert.Equal(SendOrderToFulfillmentPartnersTool.TheName, new SendOrderToFulfillmentPartnersTool().Name);
    }

    private async Task<string> SendAsync(Dictionary<string, object> arguments)
        => (await FulfillmentToolServices.InvokeAsync(_harness, new SendOrderToFulfillmentPartnersTool(), arguments)).Result;
}
