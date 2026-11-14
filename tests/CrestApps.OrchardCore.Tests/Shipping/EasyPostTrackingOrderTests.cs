using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// SEC-C17d: an EasyPost tracking update that is older than what the shipment already shows (a replayed or late
/// delivery of the same signed body) doesn't move the parcel back.
/// </summary>
public sealed class EasyPostTrackingOrderTests
{
    private const string Delivered = """
        {"description":"tracker.updated","result":{"tracking_code":"9400","status":"delivered","tracking_details":[
          {"datetime":"2026-10-18T09:00:00Z","message":"Out for delivery","status":"out_for_delivery"},
          {"datetime":"2026-10-18T15:00:00Z","message":"Delivered","status":"delivered"}]}}
        """;

    private const string InTransit = """
        {"description":"tracker.updated","result":{"tracking_code":"9400","status":"in_transit","tracking_details":[
          {"datetime":"2026-10-17T08:00:00Z","message":"In transit","status":"in_transit"}]}}
        """;

    [Fact]
    public async Task ApplyAsync_AnUpdateOlderThanWhatTheShipmentShows_IsIgnored()
    {
        // Arrange
        var shipment = new Shipment { ItemId = "s1", TrackingNumber = "9400", Status = ShipmentStatus.Delivered };
        var updater = CreateUpdater(shipment);

        await updater.ApplyAsync(JsonNode.Parse(Delivered) as JsonObject, TestContext.Current.CancellationToken);

        // Act: the older in-transit update is sent again.
        var applied = await updater.ApplyAsync(JsonNode.Parse(InTransit) as JsonObject, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(applied);
        Assert.Equal("Delivered", shipment.TrackingStatus);
        Assert.Equal("Delivered", shipment.TrackingEvents[0].Message);
    }

    [Fact]
    public async Task ApplyAsync_TheSameUpdateSentAgain_ChangesNothing()
    {
        // Arrange
        var shipment = new Shipment { ItemId = "s1", TrackingNumber = "9400", Status = ShipmentStatus.Shipped };
        var store = new Mock<IShipmentStore>();
        var updater = CreateUpdater(shipment, store);

        await updater.ApplyAsync(JsonNode.Parse(InTransit) as JsonObject, TestContext.Current.CancellationToken);

        // Act: EasyPost retries the same signed body.
        var applied = await updater.ApplyAsync(JsonNode.Parse(InTransit) as JsonObject, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(applied);
        store.Verify(value => value.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ApplyAsync_ADetailWithNoTime_IsLeftOut_SoAReplayOfItChangesNothing()
    {
        // Arrange
        const string Undated = """
            {"description":"tracker.updated","result":{"tracking_code":"9400","status":"in_transit","tracking_details":[
              {"datetime":"2026-10-17T08:00:00Z","message":"In transit","status":"in_transit"},
              {"message":"Arrived at facility","status":"in_transit"}]}}
            """;

        var shipment = new Shipment { ItemId = "s1", TrackingNumber = "9400", Status = ShipmentStatus.Shipped };
        var store = new Mock<IShipmentStore>();
        var updater = CreateUpdater(shipment, store);

        await updater.ApplyAsync(JsonNode.Parse(Undated) as JsonObject, TestContext.Current.CancellationToken);

        // Act
        var applied = await updater.ApplyAsync(JsonNode.Parse(Undated) as JsonObject, TestContext.Current.CancellationToken);

        // Assert: the detail with no time isn't stamped with the time it arrived.
        Assert.False(applied);
        Assert.Equal("In transit", Assert.Single(shipment.TrackingEvents).Message);
    }

    private static EasyPostTrackingUpdater CreateUpdater(Shipment shipment, Mock<IShipmentStore> store = null)
    {
        store ??= new Mock<IShipmentStore>();
        store.Setup(value => value.FindByTrackingNumberAsync("9400", It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc));

        return new EasyPostTrackingUpdater(store.Object, Mock.Of<IShipmentService>(), clock.Object);
    }
}
