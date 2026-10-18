using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A discount comes off the items, never shipping, and each line is taxed on what is left of it.
/// </summary>
public sealed class DiscountAllocationTests
{
    [Fact]
    public async Task AOneTimeDiscount_IsSpreadOverTheItems_InProportion_AndLeavesShippingWhole()
    {
        var invoice = Invoice();

        await Service(new DiscountLine { Code = "SAVE10", Amount = 5m, Target = DiscountTarget.OneTime })
            .ApplyDiscountsAsync(invoice, flow: null, TestContext.Current.CancellationToken);

        Assert.Equal([("mug", 3m), ("cap", 2m), ("shipping", 0m)], invoice.LineItems.Select(line => (line.ItemId, line.DiscountAmount)));
        Assert.Equal(60m, invoice.InitialPaymentAmount);
        Assert.Equal(60m, invoice.DueNow);
        Assert.Equal(5m, invoice.DiscountTotal);
    }

    [Fact]
    public async Task ADiscountLargerThanTheItems_TakesOnlyTheItems_NotTheShipping()
    {
        var invoice = Invoice();

        await Service(new DiscountLine { Code = "BIG", Amount = 70m, Target = DiscountTarget.OneTime })
            .ApplyDiscountsAsync(invoice, flow: null, TestContext.Current.CancellationToken);

        Assert.Equal(15m, invoice.InitialPaymentAmount);
        Assert.Equal(50m, invoice.DiscountTotal);
        Assert.Equal([30m, 20m, 0m], invoice.LineItems.Select(line => line.DiscountAmount));
    }

    [Fact]
    public async Task RoundingShares_AddUpToTheDiscountExactly()
    {
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 30m,
            DueNow = 30m,
            LineItems =
            [
                new CheckoutLineItem { ItemId = "a", Quantity = 1, UnitPrice = 10m },
                new CheckoutLineItem { ItemId = "b", Quantity = 1, UnitPrice = 10m },
                new CheckoutLineItem { ItemId = "c", Quantity = 1, UnitPrice = 10m },
            ],
        };

        await Service(new DiscountLine { Code = "ODD", Amount = 10m, Target = DiscountTarget.OneTime })
            .ApplyDiscountsAsync(invoice, flow: null, TestContext.Current.CancellationToken);

        Assert.Equal(10m, invoice.LineItems.Sum(line => line.DiscountAmount));
        Assert.Equal([3.33m, 3.33m, 3.34m], invoice.LineItems.Select(line => line.DiscountAmount));
    }

    [Fact]
    public async Task EachLine_IsTaxedOnWhatIsLeftOfIt()
    {
        var invoice = Invoice();

        await Service(new DiscountLine { Code = "SAVE10", Amount = 5m, Target = DiscountTarget.OneTime })
            .ApplyDiscountsAsync(invoice, flow: null, TestContext.Current.CancellationToken);

        var context = CheckoutTaxContextFactory.Create(invoice, profile: null, DateTime.UtcNow);
        var calculator = new DefaultTaxableBaseCalculator();

        // A 10% tax on $45 of items after the coupon, and on the $15 of shipping it didn't touch.
        var items = context.Items.Where(item => item.Kind != TaxableItemKind.Shipping).Sum(item => calculator.GetTaxableBase(item, context));
        var shipping = context.Items.Where(item => item.Kind == TaxableItemKind.Shipping).Sum(item => calculator.GetTaxableBase(item, context));

        Assert.Equal(45m, items);
        Assert.Equal(15m, shipping);
        Assert.Equal(4.50m, items * 0.10m);
    }

    [Fact]
    public void ACouponsBase_AndItsMinimum_CountOnlyTheItems()
    {
        var invoice = Invoice();
        var coupon = new Coupon { Code = "OVER55", Kind = CouponKind.Percentage, Percentage = 10m, MinimumAmount = 55m };

        Assert.Equal(50m, CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.OneTime));

        // $50 of items and $15 of shipping don't reach a $55 minimum.
        Assert.Equal(0m, coupon.GetDiscount(CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.OneTime), "USD"));

        // An invoice made without lines keeps its whole bucket as the base, as before.
        Assert.Equal(80m, CheckoutDiscountBase.GetAmount(new CheckoutInvoice { Currency = "USD", InitialPaymentAmount = 80m }, DiscountTarget.OneTime));
    }

    private static CheckoutInvoice Invoice()
        => new()
        {
            Currency = "USD",
            InitialPaymentAmount = 65m,
            DueNow = 65m,
            GrandTotal = 65m,
            LineItems =
            [
                new CheckoutLineItem { ItemId = "mug", Quantity = 1, UnitPrice = 30m, TaxableKind = TaxableItemKind.Physical },
                new CheckoutLineItem { ItemId = "cap", Quantity = 2, UnitPrice = 10m, TaxableKind = TaxableItemKind.Physical },
                new CheckoutLineItem { ItemId = "shipping", Quantity = 1, UnitPrice = 15m, TaxableKind = TaxableItemKind.Shipping },
            ],
        };

    private static DefaultCheckoutDiscountService Service(params DiscountLine[] discounts)
        => new([new FixedProvider(discounts)], NullLogger<DefaultCheckoutDiscountService>.Instance);

    private sealed class FixedProvider : ICheckoutDiscountProvider
    {
        private readonly DiscountLine[] _discounts;

        public FixedProvider(DiscountLine[] discounts)
            => _discounts = discounts;

        public string Key => "test";

        public Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscountLine>>([.. _discounts.Select(discount => new DiscountLine { Code = discount.Code, Amount = discount.Amount, Target = discount.Target })]);

        public Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
