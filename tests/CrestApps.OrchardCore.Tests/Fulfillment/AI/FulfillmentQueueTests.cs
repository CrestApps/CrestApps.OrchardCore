using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Models;

namespace CrestApps.OrchardCore.Tests.Fulfillment.AI;

/// <summary>
/// <see cref="FulfillmentSender.TryQueueAsync(FulfillmentRequest, bool, CancellationToken)"/> and its order overload,
/// which <c>sendOrderToFulfillmentPartners</c> uses: under the order's lock they decide on the stored state, so a request
/// that became unclear after the caller read it is left alone unless a person checked with the partner; a queued
/// request keeps the partner's message; and one stopped again before the partner is called tells nobody twice.
/// </summary>
public sealed class FulfillmentQueueTests : IAsyncLifetime
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
    public async Task TryQueue_ARequestThatBecameUnclearAfterItWasRead_IsLeftAlone_UnlessCheckedWithThePartner()
    {
        // Arrange: read while it waits, then the background send gets no clear answer.
        await _harness.RecordAsync();
        var stale = Assert.Single(await _harness.LoadRequestsAsync());
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.NeedsAttention("No answer."));
        await _harness.ProcessDueAsync();

        // Act
        var leftAlone = await QueueAsync(stale, checkedWithPartner: false);
        var afterRefusal = Assert.Single(await _harness.LoadRequestsAsync());
        var queued = await QueueAsync(stale, checkedWithPartner: true);
        var afterQueue = Assert.Single(await _harness.LoadRequestsAsync());

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Pending, stale.Status);
        Assert.False(leftAlone.TakenByAnotherWorker);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, leftAlone.Request.Status);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, afterRefusal.Status);
        Assert.Equal(FulfillmentReasons.Unclear, afterRefusal.Reason);
        Assert.Equal(FulfillmentRequestStatus.Pending, queued.Request.Status);
        Assert.Equal(FulfillmentRequestStatus.Pending, afterQueue.Status);
        Assert.Equal(FulfillmentReasons.Waiting, afterQueue.Reason);
        Assert.Equal("No answer.", afterQueue.Message);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, afterQueue.QueuedFromStatus);
        Assert.Equal(afterRefusal.ShipmentId, afterQueue.ShipmentId);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task TryQueue_AnOrder_LeavesAnUnclearRequestAlone_UnlessCheckedWithThePartner()
    {
        // Arrange
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.NeedsAttention("No answer."));
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);

        // Act
        var leftAlone = await QueueOrderAsync(checkedWithPartner: false);
        var queued = await QueueOrderAsync(checkedWithPartner: true);

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, Assert.Single(leftAlone).Status);
        Assert.Equal(FulfillmentReasons.Unclear, Assert.Single(leftAlone).Reason);
        Assert.Equal(FulfillmentRequestStatus.Pending, Assert.Single(queued).Status);
        Assert.Equal("No answer.", Assert.Single(await _harness.LoadRequestsAsync()).Message);
    }

    [Fact]
    public async Task QueuedRequest_StoppedAgainBeforeThePartnerIsCalled_DoesNotNotifyAgain()
    {
        // Arrange
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.NeedsAttention("No answer."));
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);
        var unclear = Assert.Single(await _harness.LoadRequestsAsync());
        await QueueAsync(unclear, checkedWithPartner: true);

        // The partner's feature was turned off meanwhile, so the request stops before the partner is called.
        _harness.Providers.Clear();

        // Act
        await _harness.ProcessDueAsync();

        // Assert
        var stopped = Assert.Single(await _harness.LoadRequestsAsync());

        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, stopped.Status);
        Assert.Equal(FulfillmentReasons.ProviderDisabled, stopped.Reason);
        Assert.Null(stopped.QueuedFromStatus);
        Assert.Single(_harness.Attention);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task QueuedRequest_ThePartnerLeavesUnclearAgain_NotifiesAsTheInlineSendDoes()
    {
        // Arrange
        _provider.OnSubmit = _ => Task.FromResult(FulfillmentSubmitResult.NeedsAttention("No answer."));
        await _harness.RecordAsync();
        await _harness.SendAsync(Partner);
        var unclear = Assert.Single(await _harness.LoadRequestsAsync());
        await QueueAsync(unclear, checkedWithPartner: true);

        // Act
        await _harness.ProcessDueAsync();

        // Assert
        var again = Assert.Single(await _harness.LoadRequestsAsync());

        Assert.Equal(FulfillmentReasons.Unclear, again.Reason);
        Assert.Null(again.QueuedFromStatus);
        Assert.Equal(2, _harness.Attention.Count);
        Assert.Equal(2, _provider.Calls);
    }

    private async Task<FulfillmentSendResult> QueueAsync(FulfillmentRequest request, bool checkedWithPartner)
    {
        await using var session = _harness.Store.CreateSession();
        var result = await _harness.Sender(session).TryQueueAsync(request, checkedWithPartner, FulfillmentTestHarness.Token);

        await session.SaveChangesAsync(FulfillmentTestHarness.Token);

        return result;
    }

    private async Task<IReadOnlyList<FulfillmentRequest>> QueueOrderAsync(bool checkedWithPartner)
    {
        await using var session = _harness.Store.CreateSession();
        var result = await _harness.Sender(session).TryQueueAsync(_harness.Order, checkedWithPartner, FulfillmentTestHarness.Token);

        await session.SaveChangesAsync(FulfillmentTestHarness.Token);

        return result;
    }
}
