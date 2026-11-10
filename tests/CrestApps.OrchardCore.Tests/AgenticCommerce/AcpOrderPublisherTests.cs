using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Orders;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// An ACP order event names the order's status from its shipments, each line's fulfillment state, and refunds as
/// adjustments with the amount refunded in its totals.
/// </summary>
public sealed class AcpOrderPublisherTests
{
    [Fact]
    public void Write_DerivesEachLinesStatusFromWhatWasFulfilled()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal(
            ["processing", "partial", "fulfilled", "removed"],
            json["line_items"].AsArray().Select(line => line["status"].GetValue<string>()));
        Assert.Equal(1, json["line_items"][1]["quantity"]["fulfilled"].GetValue<int>());
    }

    [Fact]
    public void Write_WithoutShipments_IsConfirmed()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(fulfilled: 0);

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal("confirmed", json["status"].GetValue<string>());
        Assert.Empty(json["fulfillments"].AsArray());
    }

    [Fact]
    public void Write_WithAShipmentOfPartOfTheOrder_IsProcessing()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(fulfilled: 1, AgentOrderTestData.Shipment("ship-1", 1, shipped: true, delivered: false));

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal("processing", json["status"].GetValue<string>());
        Assert.Equal("shipped", json["fulfillments"][0]["status"].GetValue<string>());
    }

    [Fact]
    public void Write_WithEverythingShipped_IsShipped()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(
            fulfilled: 2,
            AgentOrderTestData.Shipment("ship-1", 1, shipped: true, delivered: true),
            AgentOrderTestData.Shipment("ship-2", 1, shipped: true, delivered: false));

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal("shipped", json["status"].GetValue<string>());
    }

    [Fact]
    public void Write_WithEverythingDelivered_IsDelivered()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(fulfilled: 2, AgentOrderTestData.Shipment("ship-1", 2, shipped: true, delivered: true));

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal("delivered", json["status"].GetValue<string>());

        var fulfillment = json["fulfillments"][0];

        Assert.Equal("delivered", fulfillment["status"].GetValue<string>());
        Assert.Equal(["ship-1_shipped", "ship-1_delivered"], fulfillment["events"].AsArray().Select(entry => entry["id"].GetValue<string>()));
        Assert.Equal("UPS", fulfillment["carrier"].GetValue<string>());
    }

    [Fact]
    public void Write_ForACanceledOrder_IsCanceled()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(fulfilled: 2, AgentOrderTestData.Shipment("ship-1", 2, shipped: true, delivered: true));

        order.Canceled = true;

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        Assert.Equal("canceled", json["status"].GetValue<string>());
    }

    [Fact]
    public void Write_WritesRefundsAsAdjustmentsAndTheAmountRefunded()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        order.Refunds = [new AgentOrderRefundView { Id = "refund-1", Amount = 12.5m, Reason = "Damaged", CreatedUtc = AgentOrderTestData.CreatedUtc }];
        order.Refunded = 12.5m;

        // Act
        var json = AcpOrderPublisher.Write(order);

        // Assert
        var adjustment = Assert.Single(json["adjustments"].AsArray());

        Assert.Equal("refund", adjustment["type"].GetValue<string>());
        Assert.Equal(1250, adjustment["amount"].GetValue<long>());
        Assert.Equal("usd", adjustment["currency"].GetValue<string>());
        Assert.Equal("Damaged", adjustment["reason"].GetValue<string>());
        Assert.Equal(
            [("subtotal", 7000L), ("fulfillment", 500L), ("tax", 600L), ("total", 8100L), ("amount_refunded", 1250L)],
            json["totals"].AsArray().Select(total => (total["type"].GetValue<string>(), total["amount"].GetValue<long>())));
    }
}
