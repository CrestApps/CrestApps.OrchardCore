using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// What is sent to a partner is reserved in one pending Shipping shipment, saved with the Sending status before the
/// partner is called; only what is left to ship is sent; a refusal frees the units, an unclear outcome keeps them and
/// asks a person once; and an order is never sent twice.
/// </summary>
public sealed class FulfillmentSenderTests : IAsyncLifetime
{
    private const string Partner = "Fake";

    private readonly FulfillmentTestHarness _harness = new();
    private FulfillmentTestHarness.FakeProvider _provider;

    public async ValueTask InitializeAsync()
    {
        await _harness.InitializeAsync();
        _provider = _harness.AddProvider(Partner, "l1", "l2");
    }

    public ValueTask DisposeAsync()
        => _harness.DisposeAsync();

    [Fact]
    public async Task SendAsync_PartnerAccepts_ReservesExactlyOnePendingShipmentWithTheSentLines()
    {
        // Arrange
        await _harness.RecordAsync();

        // Act
        var first = await _harness.SendAsync(Partner);
        var second = await _harness.SendAsync(Partner);
        var shipments = await _harness.LoadShipmentsAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, first.Request.Status);
        Assert.Equal("partner-1", first.Request.ExternalOrderId);
        Assert.Equal(FulfillmentRequestStatus.Sent, second.Request.Status);
        Assert.Equal(1, _provider.Calls);

        var shipment = Assert.Single(shipments);

        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Equal(first.Request.ShipmentId, shipment.ItemId);
        Assert.Equal("Sent to Fake, order partner-1", shipment.Note);
        Assert.Equal([("l1", 2), ("l2", 1)], shipment.Lines.Select(line => (line.LineId, line.Quantity)));
        Assert.Equal([("l1", 2), ("l2", 1)], first.Request.Lines.Select(line => (line.LineId, line.Quantity)));
    }

    [Fact]
    public async Task SendAsync_SavesTheShipmentWithTheSendingStatusBeforeThePartnerIsCalled()
    {
        // Arrange
        await _harness.RecordAsync();
        (FulfillmentRequestStatus Status, string ShipmentId, int Shipments)? seenByPartner = null;

        _provider.OnSubmit = async _ =>
        {
            var requests = await _harness.LoadRequestsAsync();
            var shipments = await _harness.LoadShipmentsAsync();
            var request = Assert.Single(requests);

            seenByPartner = (request.Status, request.ShipmentId, shipments.Count(shipment => shipment.ItemId == request.ShipmentId && shipment.Status == ShipmentStatus.Pending));

            return FulfillmentSubmitResult.Sent("partner-1", null);
        };

        // Act
        await _harness.SendAsync(Partner);

        // Assert
        Assert.NotNull(seenByPartner);
        Assert.Equal(FulfillmentRequestStatus.Sending, seenByPartner.Value.Status);
        Assert.False(string.IsNullOrEmpty(seenByPartner.Value.ShipmentId));
        Assert.Equal(1, seenByPartner.Value.Shipments);
    }

    [Fact]
    public async Task ShipAsync_TrackingTypedOnTheReservedShipment_FulfillsTheOrder()
    {
        // Arrange
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);

        // Act
        await using (var session = _harness.Store.CreateSession())
        {
            var shipment = Assert.Single(await new ShipmentStore(session).ListByOrderAsync(_harness.Order.ItemId, FulfillmentTestHarness.Token));
            var shipped = await _harness.Shipments(session).ShipAsync(shipment, new ShipmentDetails("UPS", "1Z999", "https://tracking.example.com/1Z999"), FulfillmentTestHarness.Token);

            Assert.True(shipped.Succeeded);
            await session.SaveChangesAsync(FulfillmentTestHarness.Token);
        }

        // Assert
        Assert.Equal(OrderFulfillmentStatus.Fulfilled, _harness.Order.FulfillmentStatus);
        Assert.Equal(ShipmentStatus.Shipped, Assert.Single(await _harness.LoadShipmentsAsync()).Status);
    }

    [Fact]
    public async Task SendAsync_SomeUnitsAlreadyShipped_SendsOnlyWhatIsLeft()
    {
        // Arrange
        _harness.Order.Lines[0].Quantity = 3;
        await _harness.SaveShipmentAsync(new Shipment
        {
            ItemId = "shipped-by-hand",
            OrderId = _harness.Order.ItemId,
            OrderNumber = _harness.Order.OrderNumber,
            Number = 1,
            Status = ShipmentStatus.Shipped,
            Lines = [new ShipmentLine { LineId = "l1", Title = "Tee", Quantity = 1 }],
            CreatedUtc = _harness.Now,
            ModifiedUtc = _harness.Now,
        });
        await _harness.RecordAsync();

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, result.Request.Status);
        Assert.Equal([("l1", 2), ("l2", 1)], Assert.Single(_provider.Received).Lines.Select(line => (line.LineId, line.Quantity)));
    }

    [Fact]
    public async Task SendAsync_PartnerRefuses_FreesTheUnitsAndAsksAPerson()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.Refused("The address is not valid."));

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Request.Status);
        Assert.Equal(FulfillmentReasons.Refused, result.Request.Reason);
        Assert.Equal("The address is not valid.", result.Request.Message);
        Assert.Null(result.Request.ShipmentId);
        Assert.Empty(await _harness.LoadShipmentsAsync());
        Assert.Single(_harness.Attention);
    }

    [Fact]
    public async Task SendAsync_PartnerFailsUnexpectedly_KeepsTheShipmentAndSendingAgainReusesIt()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => throw new InvalidOperationException("Connection reset after the request was written.");

        // Act
        var unclear = await _harness.SendAsync(Partner);
        var processed = await _harness.ProcessDueAsync();
        var keptShipment = Assert.Single(await _harness.LoadShipmentsAsync());

        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.Sent("partner-2", null));

        var again = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, unclear.Request.Status);
        Assert.Equal(FulfillmentReasons.Unclear, unclear.Request.Reason);
        Assert.Equal(0, processed);
        Assert.Equal(ShipmentStatus.Pending, keptShipment.Status);
        Assert.Equal(FulfillmentRequestStatus.Sent, again.Request.Status);
        Assert.Equal(keptShipment.ItemId, again.Request.ShipmentId);
        Assert.Single(await _harness.LoadShipmentsAsync());
        Assert.Equal(2, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_PartnerThrows_OneRequestAndOneNotification_NotRepeatedByTheBackgroundTask()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => throw new HttpRequestException("Gateway timeout.");

        // Act
        await _harness.SendAsync(Partner);
        await _harness.ProcessDueAsync();
        _harness.Now = _harness.Now.AddHours(1);
        await _harness.ProcessDueAsync();

        // Assert
        var request = Assert.Single(await _harness.LoadRequestsAsync());
        var notified = Assert.Single(_harness.Attention);

        Assert.Equal(request.ItemId, notified.ItemId);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, request.Status);
        Assert.Equal(1, _provider.Calls);

        await using var session = _harness.Store.CreateSession();
        var store = new FulfillmentRequestStore(session);
        var (needsAttention, total) = await store.ListPageAsync(FulfillmentRequestStatus.NeedsAttention, null, 0, 10, FulfillmentTestHarness.Token);
        var (searched, _) = await store.ListPageAsync(null, "261117", 0, 10, FulfillmentTestHarness.Token);
        var (sent, _) = await store.ListPageAsync(FulfillmentRequestStatus.Sent, null, 0, 10, FulfillmentTestHarness.Token);

        Assert.Equal(1, total);
        Assert.Equal(request.ItemId, Assert.Single(needsAttention).ItemId);
        Assert.Single(searched);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task SendAsync_PartnerDoesNotAnswerInTime_IsUnclearAndKeepsTheShipment()
    {
        // Arrange
        _harness.SubmitTimeout = TimeSpan.FromMilliseconds(100);
        await _harness.RecordAsync();
        _provider.OnSubmit = async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);

            return FulfillmentSubmitResult.Sent("never", null);
        };

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Request.Status);
        Assert.Equal(FulfillmentReasons.Unclear, result.Request.Reason);
        Assert.Equal(ShipmentStatus.Pending, Assert.Single(await _harness.LoadShipmentsAsync()).Status);
    }

    [Fact]
    public async Task SendAsync_PartnerBusy_WaitsAndTriesAgainLaterWithTheSameShipment()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.RetryLater("Busy."));

        // Act
        var busy = await _harness.SendAsync(Partner);
        var tooSoon = await _harness.ProcessDueAsync();

        _harness.Now = _harness.Now.AddMinutes(5);
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.Sent("partner-2", null));

        var later = await _harness.ProcessDueAsync();
        var final = Assert.Single(await _harness.LoadRequestsAsync());

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Pending, busy.Request.Status);
        Assert.Equal(0, tooSoon);
        Assert.Equal(1, later);
        Assert.Equal(FulfillmentRequestStatus.Sent, final.Status);
        Assert.Equal(busy.Request.ShipmentId, final.ShipmentId);
        Assert.Single(await _harness.LoadShipmentsAsync());
        Assert.Equal(2, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_PartnerBusyThenOrderCanceled_FreesTheUnits()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.RetryLater("Busy."));
        await _harness.SendAsync(Partner);
        _harness.Order.Status = OrderStatus.Canceled;
        _harness.Now = _harness.Now.AddMinutes(5);

        // Act
        await _harness.ProcessDueAsync();

        // Assert
        var request = Assert.Single(await _harness.LoadRequestsAsync());

        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, request.Status);
        Assert.Equal(FulfillmentReasons.NotPaid, request.Reason);
        Assert.Empty(await _harness.LoadShipmentsAsync());
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_ReservedShipmentShippedByHand_SendsNothing()
    {
        // Arrange
        await _harness.RecordAsync();
        _provider.OnSubmit = _ => throw new InvalidOperationException("Lost the answer.");
        await _harness.SendAsync(Partner);

        await using (var session = _harness.Store.CreateSession())
        {
            var shipment = Assert.Single(await new ShipmentStore(session).ListByOrderAsync(_harness.Order.ItemId, FulfillmentTestHarness.Token));

            await _harness.Shipments(session).ShipAsync(shipment, new ShipmentDetails("UPS", "1Z1", "https://tracking.example.com/1Z1"), FulfillmentTestHarness.Token);
            await session.SaveChangesAsync(FulfillmentTestHarness.Token);
        }

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NothingToSend, result.Request.Status);
        Assert.Equal(FulfillmentReasons.ShippedByHand, result.Request.Reason);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_AnotherWorkerHoldsTheOrder_LeavesItAlone()
    {
        // Arrange
        await _harness.RecordAsync();
        _harness.LockAvailable = false;

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.True(result.TakenByAnotherWorker);
        Assert.Equal(0, _provider.Calls);
        Assert.Empty(await _harness.LoadShipmentsAsync());
    }

    [Fact]
    public async Task SendAsync_TwoRecordsOfTheSameOrderAndPartner_OnlyOneIsSent()
    {
        // Arrange
        await using (var session = _harness.Store.CreateSession())
        {
            var store = new FulfillmentRequestStore(session);

            await store.CreateAsync(_harness.NewRequest("request-a", Partner), FulfillmentTestHarness.Token);
            await store.CreateAsync(_harness.NewRequest("request-b", Partner), FulfillmentTestHarness.Token);
            await session.SaveChangesAsync(FulfillmentTestHarness.Token);
        }

        // Act
        var a = await _harness.SendByIdAsync("request-a");
        var b = await _harness.SendByIdAsync("request-b");

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, a.Request.Status);
        Assert.Equal(FulfillmentRequestStatus.NothingToSend, b.Request.Status);
        Assert.Equal(FulfillmentReasons.AlreadySent, b.Request.Reason);
        Assert.Equal(1, _provider.Calls);
        Assert.Single(await _harness.LoadShipmentsAsync());
    }

    [Fact]
    public async Task RecordAsync_CalledTwice_ReusesTheRequest()
    {
        // Act
        await _harness.RecordAsync();
        await _harness.RecordAsync();

        // Assert
        Assert.Single(await _harness.LoadRequestsAsync());
    }

    [Fact]
    public async Task SendAsync_OrderNoLongerPaid_AsksAPersonWithoutCallingThePartner()
    {
        // Arrange
        await _harness.RecordAsync();
        _harness.Order.Status = OrderStatus.Canceled;

        // Act
        var result = await _harness.SendAsync(Partner);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Request.Status);
        Assert.Equal(FulfillmentReasons.NotPaid, result.Request.Reason);
        Assert.Equal([nameof(OrderStatus.Canceled)], result.Request.ReasonArguments);
        Assert.Equal(0, _provider.Calls);
        Assert.Empty(await _harness.LoadShipmentsAsync());
    }

    [Fact]
    public async Task FlagStuckAsync_SendInterrupted_AsksAPersonOnceAndKeepsTheShipment()
    {
        // Arrange
        await using (var session = _harness.Store.CreateSession())
        {
            var request = _harness.NewRequest("request-stuck", Partner);

            request.Status = FulfillmentRequestStatus.Sending;
            request.ShipmentId = "shipment-stuck";
            request.ModifiedUtc = _harness.Now.AddMinutes(-20);

            await new FulfillmentRequestStore(session).CreateAsync(request, FulfillmentTestHarness.Token);
            await session.SaveChangesAsync(FulfillmentTestHarness.Token);
        }

        // Act
        int flagged;
        int flaggedAgain;

        await using (var session = _harness.Store.CreateSession())
        {
            flagged = await _harness.Sender(session).FlagStuckAsync(FulfillmentTestHarness.Token);
        }

        await using (var session = _harness.Store.CreateSession())
        {
            flaggedAgain = await _harness.Sender(session).FlagStuckAsync(FulfillmentTestHarness.Token);
        }

        var result = Assert.Single(await _harness.LoadRequestsAsync());

        // Assert
        Assert.Equal(1, flagged);
        Assert.Equal(0, flaggedAgain);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Status);
        Assert.Equal(FulfillmentReasons.Interrupted, result.Reason);
        Assert.Equal("shipment-stuck", result.ShipmentId);
        Assert.Single(_harness.Attention);
        Assert.Equal(0, _provider.Calls);
    }
}
