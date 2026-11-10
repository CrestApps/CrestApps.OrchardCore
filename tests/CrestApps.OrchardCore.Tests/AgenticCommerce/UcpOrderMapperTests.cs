using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// A UCP order shows each line's fulfillment state, its shipments as events, refunds as adjustments that take money
/// back, and totals whose parts add up.
/// </summary>
public sealed class UcpOrderMapperTests
{
    private const string Version = AgenticCommerceConstants.Versions.Ucp20260825;

    [Fact]
    public void Write_DerivesEachLinesStatusFromWhatWasFulfilled()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        // Act
        var json = UcpOrderMapper.Write(order, Version);

        // Assert
        Assert.Equal(
            [("processing", "processing"), ("partial", "partial"), ("fulfilled", "fulfilled"), ("removed", "removed")],
            json["line_items"].AsArray().Select(line => (line["id"].GetValue<string>(), line["status"].GetValue<string>())));

        var partial = json["line_items"][1]["quantity"];

        Assert.Equal(2, partial["original"].GetValue<int>());
        Assert.Equal(2, partial["total"].GetValue<int>());
        Assert.Equal(1, partial["fulfilled"].GetValue<int>());
    }

    [Fact]
    public void Write_TotalsAddUpToTheTotal()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        // Act
        var json = UcpOrderMapper.Write(order, Version);

        // Assert
        var amounts = json["totals"].AsArray().ToDictionary(total => total["type"].GetValue<string>(), total => total["amount"].GetValue<long>());

        Assert.Equal(-1000, amounts["discount"]);
        Assert.Equal(600, amounts["tax"]);
        Assert.Equal(8100, amounts["total"]);
        Assert.Equal(amounts["total"], amounts.Where(entry => entry.Key != "total").Sum(entry => entry.Value));
    }

    [Fact]
    public void Write_AnAmountTheOrderDoesNotExplain_IsAFeeNotTax()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        order.Total += 2.5m;

        // Act
        var json = UcpOrderMapper.Write(order, Version);

        // Assert
        var amounts = json["totals"].AsArray().ToDictionary(total => total["type"].GetValue<string>(), total => total["amount"].GetValue<long>());

        Assert.Equal(600, amounts["tax"]);
        Assert.Equal(250, amounts["fee"]);
        Assert.Equal(amounts["total"], amounts.Where(entry => entry.Key != "total").Sum(entry => entry.Value));
    }

    [Fact]
    public void Write_WritesRefundsAsAdjustmentsWithNegativeTotals()
    {
        // Arrange
        var order = AgentOrderTestData.OrderWithEveryLineStatus();

        order.Refunds = [new AgentOrderRefundView { Id = "refund-1", Amount = 12.5m, Reason = "Damaged", CreatedUtc = AgentOrderTestData.CreatedUtc.AddDays(5) }];

        // Act
        var json = UcpOrderMapper.Write(order, Version);

        // Assert
        var adjustment = Assert.Single(json["adjustments"].AsArray());

        Assert.Equal("refund-1", adjustment["id"].GetValue<string>());
        Assert.Equal("refund", adjustment["type"].GetValue<string>());
        Assert.Equal("completed", adjustment["status"].GetValue<string>());
        Assert.Equal("Damaged", adjustment["description"].GetValue<string>());
        Assert.Equal("2026-10-06T09:00:00Z", adjustment["occurred_at"].GetValue<string>());
        Assert.Equal(-1250, adjustment["totals"][0]["amount"].GetValue<long>());
    }

    [Fact]
    public void Write_WithoutRefunds_HasNoAdjustments()
    {
        // Act
        var json = UcpOrderMapper.Write(AgentOrderTestData.OrderWithEveryLineStatus(), Version);

        // Assert
        Assert.False(json.ContainsKey("adjustments"));
    }

    [Fact]
    public void Write_WritesShippedAndDeliveredEventsAndACancellation()
    {
        // Arrange
        var order = AgentOrderTestData.OrderOfOneLine(
            fulfilled: 2,
            AgentOrderTestData.Shipment("ship-1", 2, shipped: true, delivered: true));

        order.Canceled = true;

        // Act
        var json = UcpOrderMapper.Write(order, Version);

        // Assert
        Assert.Equal(
            ["ship-1_shipped", "ship-1_delivered", "canceled"],
            json["fulfillment"]["events"].AsArray().Select(entry => entry["id"].GetValue<string>()));
        Assert.Equal("1Z999", json["fulfillment"]["events"][0]["tracking_number"].GetValue<string>());
        Assert.Equal(Version, json["ucp"]["capabilities"][UcpConstants.Capabilities.Order][0]["version"].GetValue<string>());
        Assert.Equal("shipping", json["fulfillment"]["expectations"][0]["method_type"].GetValue<string>());
    }
}
