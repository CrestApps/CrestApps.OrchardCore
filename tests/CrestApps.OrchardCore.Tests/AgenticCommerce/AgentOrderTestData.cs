using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce.Orders;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Hand-built agent order views for the UCP and ACP order mapping tests.
/// </summary>
internal static class AgentOrderTestData
{
    public static readonly DateTime CreatedUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Returns an order with one line in each fulfillment state: processing, partial, fulfilled and removed.
    /// </summary>
    public static AgentOrderView OrderWithEveryLineStatus()
        => new()
        {
            Id = "order-1",
            Number = "1001",
            CheckoutId = "chk_1",
            PermalinkUrl = "https://store.example.com/orders/1001",
            Currency = "USD",
            CreatedUtc = CreatedUtc,
            ShippingAddress = new Address { Name = "Ada Lovelace", AddressLine1 = "1 First St", Country = "US" },
            ShippingMethod = "Standard",
            Lines =
            [
                Line("processing", current: 2, fulfilled: 0),
                Line("partial", current: 2, fulfilled: 1),
                Line("fulfilled", current: 2, fulfilled: 2),
                Line("removed", current: 0, fulfilled: 0),
            ],
            Subtotal = 80m,
            Discount = 10m,
            Shipping = 5m,
            Tax = 6m,
            Total = 81m,
        };

    /// <summary>
    /// Returns an order of one line, with its shipments and how many of the line they hold.
    /// </summary>
    public static AgentOrderView OrderOfOneLine(int fulfilled, params AgentOrderShipmentView[] shipments)
        => new()
        {
            Id = "order-2",
            Number = "1002",
            CheckoutId = "chk_2",
            Currency = "USD",
            CreatedUtc = CreatedUtc,
            Lines = [Line("line-1", current: 2, fulfilled: fulfilled)],
            Shipments = shipments,
            Subtotal = 20m,
            Total = 20m,
        };

    /// <summary>
    /// Returns a shipment of the order's line.
    /// </summary>
    public static AgentOrderShipmentView Shipment(string id, int quantity, bool shipped, bool delivered)
        => new()
        {
            Id = id,
            Carrier = "UPS",
            TrackingNumber = "1Z999",
            ShippedUtc = shipped ? CreatedUtc.AddDays(1) : null,
            DeliveredUtc = delivered ? CreatedUtc.AddDays(3) : null,
            Lines = new Dictionary<string, int> { ["line-1"] = quantity },
        };

    private static AgentOrderLineView Line(string id, int current, int fulfilled)
        => new()
        {
            Id = id,
            ItemId = "item-" + id,
            Title = "Widget " + id,
            UnitPrice = 10m,
            Quantity = 2,
            CurrentQuantity = current,
            FulfilledQuantity = fulfilled,
            Total = 10m * current,
        };
}
