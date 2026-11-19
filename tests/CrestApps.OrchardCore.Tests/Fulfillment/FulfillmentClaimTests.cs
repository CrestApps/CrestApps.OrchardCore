using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// The engine decides which partner makes which line: each partner is sent only the lines it claims, a line two
/// partners claim goes to a person and neither partner is called, and a partner that claims nothing is not called.
/// </summary>
public sealed class FulfillmentClaimTests : IAsyncLifetime
{
    private readonly FulfillmentTestHarness _harness = new();

    public ValueTask InitializeAsync()
        => new(_harness.InitializeAsync());

    public ValueTask DisposeAsync()
        => _harness.DisposeAsync();

    [Fact]
    public async Task SendAsync_TwoPartnersClaimDifferentLines_EachIsSentOnlyItsOwn()
    {
        // Arrange
        var tees = _harness.AddProvider("Tees", "l1");
        var mugs = _harness.AddProvider("Mugs", "l2");

        await _harness.RecordAsync();

        // Act
        var teesResult = await _harness.SendAsync("Tees");
        var mugsResult = await _harness.SendAsync("Mugs");
        var shipments = await _harness.LoadShipmentsAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, teesResult.Request.Status);
        Assert.Equal(FulfillmentRequestStatus.Sent, mugsResult.Request.Status);
        Assert.Equal([("l1", 2)], Assert.Single(tees.Received).Lines.Select(line => (line.LineId, line.Quantity)));
        Assert.Equal([("l2", 1)], Assert.Single(mugs.Received).Lines.Select(line => (line.LineId, line.Quantity)));
        Assert.Equal(2, shipments.Count);
        Assert.Equal(["l1"], shipments.Single(shipment => shipment.ItemId == teesResult.Request.ShipmentId).Lines.Select(line => line.LineId));
        Assert.Equal(["l2"], shipments.Single(shipment => shipment.ItemId == mugsResult.Request.ShipmentId).Lines.Select(line => line.LineId));
    }

    [Fact]
    public async Task SendAsync_TwoPartnersClaimTheSameLine_AsksAPersonAndCallsNeither()
    {
        // Arrange
        var first = _harness.AddProvider("First", "l1");
        var second = _harness.AddProvider("Second", "l1");

        await _harness.RecordAsync();

        // Act
        var firstResult = await _harness.SendAsync("First");
        var secondResult = await _harness.SendAsync("Second");

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, firstResult.Request.Status);
        Assert.Equal(FulfillmentReasons.ConflictingClaims, firstResult.Request.Reason);
        Assert.Equal(["Tee", "First, Second"], firstResult.Request.ReasonArguments);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, secondResult.Request.Status);
        Assert.Equal(0, first.Calls);
        Assert.Equal(0, second.Calls);
        Assert.Empty(await _harness.LoadShipmentsAsync());
        Assert.Equal(2, _harness.Attention.Count);
    }

    [Fact]
    public async Task SendAsync_PartnerClaimsNothing_HasNothingToSendAndIsNotCalled()
    {
        // Arrange
        var partner = _harness.AddProvider("Idle");

        await _harness.RecordAsync();

        // Act
        var result = await _harness.SendAsync("Idle");

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NothingToSend, result.Request.Status);
        Assert.Equal(FulfillmentReasons.NotClaimed, result.Request.Reason);
        Assert.Equal(0, partner.Calls);
        Assert.Empty(await _harness.LoadShipmentsAsync());
        Assert.Empty(_harness.Attention);
    }

    [Fact]
    public async Task SendAsync_PartnerCannotSayWhatItMakes_AsksAPersonWithoutReservingAnything()
    {
        // Arrange
        var partner = _harness.AddProvider("Broken");

        partner.OnClaim = _ => throw new HttpRequestException("Catalog unavailable.");
        await _harness.RecordAsync();

        // Act
        var result = await _harness.SendAsync("Broken");

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Request.Status);
        Assert.Equal(FulfillmentReasons.ClaimFailed, result.Request.Reason);
        Assert.Equal(0, partner.Calls);
        Assert.Empty(await _harness.LoadShipmentsAsync());
    }

    [Fact]
    public async Task AssignAsync_ClaimLargerThanWhatIsLeft_IsCapped()
    {
        // Arrange
        var greedy = _harness.AddProvider("Greedy");

        greedy.OnClaim = _ => [new FulfillmentLineClaim("l1", 10), new FulfillmentLineClaim("unknown", 1)];

        var order = FulfillmentOrderBuilder.ForLines(_harness.Order, new Dictionary<string, int> { ["l1"] = 2, ["l2"] = 1 });
        var assigner = new FulfillmentLineAssigner(_harness.Providers, NullLogger<FulfillmentLineAssigner>.Instance);

        // Act
        var assignment = await assigner.AssignAsync(greedy, order, FulfillmentTestHarness.Token);

        // Assert
        Assert.False(assignment.HasConflict);
        Assert.Equal(2, Assert.Single(assignment.Lines).Value);
    }

    [Fact]
    public async Task AssignAsync_AnotherPartnerFails_StillAssignsThisPartnersLines()
    {
        // Arrange
        var working = _harness.AddProvider("Working", "l1");
        var broken = _harness.AddProvider("Broken");

        broken.OnClaim = _ => throw new HttpRequestException("Catalog unavailable.");

        var order = FulfillmentOrderBuilder.ForLines(_harness.Order, new Dictionary<string, int> { ["l1"] = 2 });
        var assigner = new FulfillmentLineAssigner(_harness.Providers, NullLogger<FulfillmentLineAssigner>.Instance);

        // Act
        var assignment = await assigner.AssignAsync(working, order, FulfillmentTestHarness.Token);

        // Assert
        Assert.Equal(("l1", 2), (Assert.Single(assignment.Lines).Key, assignment.Lines["l1"]));
    }
}
