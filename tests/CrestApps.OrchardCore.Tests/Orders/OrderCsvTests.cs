using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Products.Core.Transfer;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// The orders export: a row per line, the order's totals on its first line only.
/// </summary>
public sealed class OrderCsvTests
{
    [Fact]
    public void EachLineIsARow_AndTheTotalsAreCountedOnce()
    {
        var order = new Order
        {
            OrderNumber = "261019-AAAA",
            CreatedUtc = new DateTime(2026, 10, 19, 17, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Paid,
            CustomerName = "Sam, Buyer",
            CustomerEmail = "sam@example.com",
            Currency = "USD",
            Subtotal = 45m,
            Total = 51m,
            Shipping = new OrderShipping { Amount = 6m },
            Lines =
            [
                new OrderLine { Title = "Mug", Sku = "MUG", Quantity = 2, UnitPrice = 10m, LineTotal = 20m, AddOns = [new OrderLineAddOn { Label = "Name", Value = "Sam" }] },
                new OrderLine { Title = "Cap", Sku = "CAP", Quantity = 1, UnitPrice = 25m, LineTotal = 25m },
            ],
        };

        var (headers, rows) = CsvTable.Parse(OrderCsv.Write([order], utc => utc.AddHours(-7)));

        Assert.Equal(OrderCsv.Columns, headers);
        Assert.Equal(2, rows.Count);
        Assert.Equal("2026-10-19 10:00", rows[0]["Placed"]);
        Assert.Equal("Sam, Buyer", rows[0]["Customer"]);
        Assert.Equal("Mug · Name: Sam", rows[0]["Item"]);
        Assert.Equal(("51.00", "6.00"), (rows[0]["Total"], rows[0]["Shipping"]));
        Assert.Equal(("Cap", string.Empty), (rows[1]["Item"], rows[1]["Total"]));
    }
}
