using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// What an order earns, what points are worth, and the record of earning and taking back.
/// </summary>
public sealed class LoyaltyTests
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly LoyaltySettings _settings = new() { PointsPerUnit = 2m, PointValue = 0.01m, MinimumPointsToRedeem = 100 };

    [Fact]
    public void AnOrder_EarnsOnItsGoodsAfterDiscounts_NotShippingOrTax()
    {
        var order = new Order
        {
            Subtotal = 120.75m,
            Total = 140m,
            Discounts =
            [
                new OrderDiscount { Amount = 20m },
                new OrderDiscount { Amount = 9m, Target = CrestApps.OrchardCore.Checkout.Models.DiscountTarget.Shipping },
            ],
        };

        // $100.75 of goods after the discount on them, at 2 points a dollar; free shipping takes nothing off.
        Assert.Equal(201, LoyaltyService.GetPointsFor(order, _settings));

        // What points paid earns no more points.
        order.Tenders = [new OrderTender { ProviderKey = LoyaltyService.TenderKey, Amount = 50.75m }];
        Assert.Equal(100, LoyaltyService.GetPointsFor(order, _settings));
    }

    [Fact]
    public void PointsAreWorthTheirValue_ToTheCentBelow_AndAnAmountTakesThePointsAbove()
    {
        Assert.Equal(23.45m, LoyaltyService.GetValue(2345, _settings, "USD"));
        Assert.Equal(1235, LoyaltyService.GetPointsWorth(12.341m, _settings));
    }

    [Fact]
    public void Points_NeverGoBelowZero_AndAnOrdersEarnedPoints_CountWhatWasTakenBack()
    {
        var account = new LoyaltyAccount();

        account.ChangePoints(200, LoyaltyActivityKind.Earned, "Earned", _now, orderId: "order-1");
        account.ChangePoints(-50, LoyaltyActivityKind.Reversed, "Refund", _now, orderId: "order-1");

        Assert.Equal(150, account.GetEarnedFor("order-1"));

        Assert.Equal(-150, account.ChangePoints(-500, LoyaltyActivityKind.Spent, "Spent", _now));
        Assert.Equal(0, account.Points);
    }
}
