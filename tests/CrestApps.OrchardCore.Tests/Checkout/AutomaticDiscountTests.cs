using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Discounts that apply by themselves without a code, buy X get Y offers, and minimum quantities.
/// </summary>
public sealed class AutomaticDiscountTests
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuyTwoGetOneFree_OnThreeShirts_MakesTheCheapestFree()
    {
        var coupon = BuyXGetY(buy: 2, get: 1, percentage: 100m);

        var amount = coupon.GetBuyXGetYDiscount(
        [
            Line("tee", "product-tee", quantity: 2, price: 20m),
            Line("tank", "product-tank", quantity: 1, price: 15m),
        ], out var shares);

        Assert.Equal(15m, amount);
        Assert.Equal(15m, Assert.Single(shares, share => share.Key == "tank").Value);
    }

    [Fact]
    public void BuyTwoGetOneFree_OnSixItems_MakesTheTwoCheapestFree_UnlessLimitedToOncePerOrder()
    {
        var coupon = BuyXGetY(buy: 2, get: 1, percentage: 100m);
        CheckoutLineItem[] lines =
        [
            Line("a", "product-a", quantity: 2, price: 30m),
            Line("b", "product-b", quantity: 2, price: 20m),
            Line("c", "product-c", quantity: 2, price: 10m),
        ];

        Assert.Equal(20m, coupon.GetBuyXGetYDiscount(lines, out _));

        coupon.MaxApplicationsPerOrder = 1;

        Assert.Equal(10m, coupon.GetBuyXGetYDiscount(lines, out _));
    }

    [Fact]
    public void BuyShoes_GetSocksHalfOff_CountsOnlyTheShoesBought_AndDiscountsOnlyTheSocks()
    {
        var coupon = BuyXGetY(buy: 2, get: 1, percentage: 50m);
        coupon.BuyProductIds = ["product-shoe"];
        coupon.ProductIds = ["product-sock"];

        var amount = coupon.GetBuyXGetYDiscount(
        [
            Line("shoe", "product-shoe", quantity: 2, price: 80m),
            Line("sock", "product-sock", quantity: 2, price: 12m),
        ], out var shares);

        Assert.Equal(6m, amount);
        Assert.Equal(["sock"], shares.Keys);

        // One shoe isn't enough.
        Assert.Equal(0m, coupon.GetBuyXGetYDiscount(
        [
            Line("shoe", "product-shoe", quantity: 1, price: 80m),
            Line("sock", "product-sock", quantity: 2, price: 12m),
        ], out _));
    }

    [Fact]
    public async Task AnAutomaticDiscount_AppliesWithoutACode_AndTheFreeItemCarriesTheWholeDiscount()
    {
        var coupon = BuyXGetY(buy: 1, get: 1, percentage: 100m);
        coupon.Method = CouponMethod.Automatic;
        coupon.Description = "Buy one, get one free";

        var invoice = Invoice();
        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };

        await ApplyAsync(invoice, session, automatic: [coupon]);

        var discount = Assert.Single(invoice.Discounts);
        Assert.True(discount.IsAutomatic);
        Assert.Equal("Buy one, get one free", discount.Description);
        Assert.Equal(10m, discount.Amount);
        Assert.Equal(DiscountClass.Product, discount.GetClass());

        // Two caps at 10 and a mug at 30: one cap pays for the other, and only the cap line is discounted.
        Assert.Equal(10m, invoice.LineItems.Single(line => line.ItemId == "cap").DiscountAmount);
        Assert.Equal(0m, invoice.LineItems.Single(line => line.ItemId == "mug").DiscountAmount);
    }

    [Fact]
    public async Task AnAutomaticDiscount_StaysOff_WhenItWasDeclined_OrTheBuyerHasHadItAsOftenAsItAllows()
    {
        var coupon = new Coupon { Code = "AUTO-SPRING", Method = CouponMethod.Automatic, Description = "Spring sale", Kind = CouponKind.Percentage, Percentage = 10m };

        var declined = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        var applied = new CheckoutCoupon();
        applied.Decline("auto-spring");
        declined.Put(applied);

        var invoice = Invoice();
        await ApplyAsync(invoice, declined, automatic: [coupon]);
        Assert.Empty(invoice.Discounts);

        invoice = Invoice();
        await ApplyAsync(invoice, new CheckoutSession { SessionId = "session-2", Currency = "USD" }, automatic: [coupon], availability: CouponAvailability.CustomerLimitReached);
        Assert.Empty(invoice.Discounts);
    }

    [Fact]
    public async Task TypingAnAutomaticDiscountsCode_DoesNotApplyItTwice()
    {
        var coupon = new Coupon { Code = "AUTO-SPRING", Method = CouponMethod.Automatic, Description = "Spring sale", Kind = CouponKind.Percentage, Percentage = 10m };
        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutCoupon { Codes = ["AUTO-SPRING"] });

        var invoice = Invoice();
        await ApplyAsync(invoice, session, automatic: [coupon], byCode: coupon);

        Assert.Equal(5m, Assert.Single(invoice.Discounts).Amount);
    }

    [Fact]
    public async Task AMinimumQuantity_CountsTheItemsTheCouponIsFor()
    {
        var coupon = new Coupon { Code = "THREE", Kind = CouponKind.Percentage, Percentage = 10m, MinimumQuantity = 3 };

        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutCoupon { Codes = ["THREE"] });

        // A mug and two caps make three items.
        var invoice = Invoice();
        await ApplyAsync(invoice, session, byCode: coupon);
        Assert.Equal(5m, Assert.Single(invoice.Discounts).Amount);

        // Limited to caps, there are only two.
        coupon.ProductIds = ["product-cap"];
        invoice = Invoice();
        await ApplyAsync(invoice, session, byCode: coupon);
        Assert.Empty(invoice.Discounts);
    }

    [Fact]
    public async Task ThePaymentGate_TakesAnAutomaticDiscountOffForGood_WhenItRanOut()
    {
        var redemptions = new Mock<ICouponRedemptionService>();
        redemptions
            .Setup(service => service.HoldAsync(It.IsAny<CheckoutFlow>(), It.IsAny<DiscountLine>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CouponAvailability.UsedUp);

        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            Discounts = [new DiscountLine { ProviderKey = CouponDiscountProvider.ProviderKey, Code = "AUTO-SPRING", Description = "Spring sale", IsAutomatic = true, Amount = 5m }],
        });

        var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(session), "test");

        await new CouponHoldPaymentGate(redemptions.Object, CreateBuilder(), new PassThroughLocalizer<CouponHoldPaymentGate>())
            .BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
        Assert.Contains("Spring sale", context.ErrorMessage, StringComparison.Ordinal);
        Assert.True(session.TryGet<CheckoutCoupon>(out var applied));
        Assert.True(applied.IsDeclined("AUTO-SPRING"));
        Assert.Empty(applied.GetCodes());
    }

    private static Coupon BuyXGetY(int buy, int get, decimal percentage)
        => new()
        {
            Code = "BXGY",
            Kind = CouponKind.BuyXGetY,
            BuyQuantity = buy,
            GetQuantity = get,
            Percentage = percentage,
        };

    private static CheckoutLineItem Line(string itemId, string productId, int quantity, decimal price)
        => new() { ItemId = itemId, ProductId = productId, Quantity = quantity, UnitPrice = price, TaxableKind = TaxableItemKind.Physical };

    private static async Task ApplyAsync(CheckoutInvoice invoice, CheckoutSession session, Coupon[] automatic = null, Coupon byCode = null, CouponAvailability availability = CouponAvailability.Available)
    {
        foreach (var coupon in (automatic ?? []).Append(byCode).Where(coupon => coupon is not null))
        {
            coupon.ItemId ??= coupon.Code;
            coupon.IsEnabled = true;
        }

        var store = new Mock<ICouponStore>();
        store.Setup(value => value.ListAutomaticAsync(It.IsAny<CancellationToken>())).ReturnsAsync(automatic ?? []);

        if (byCode is not null)
        {
            store.Setup(value => value.GetByCodeAsync(byCode.Code, It.IsAny<CancellationToken>())).ReturnsAsync(byCode);
        }

        var redemptions = new Mock<ICouponRedemptionService>();
        redemptions
            .Setup(service => service.GetAvailabilityAsync(It.IsAny<Coupon>(), It.IsAny<CheckoutFlow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(availability);

        var provider = new CouponDiscountProvider(
            store.Object,
            new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider()),
            redemptions.Object,
            new TestClock(_now));

        await new DefaultCheckoutDiscountService([provider], NullLogger<DefaultCheckoutDiscountService>.Instance)
            .ApplyDiscountsAsync(invoice, new CheckoutFlow(session), TestContext.Current.CancellationToken);
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
                new CheckoutLineItem { ItemId = "mug", ProductId = "product-mug", CategoryIds = ["term-kitchen"], Quantity = 1, UnitPrice = 30m, TaxableKind = TaxableItemKind.Physical },
                new CheckoutLineItem { ItemId = "cap", ProductId = "product-cap", CategoryIds = ["term-hats"], Quantity = 2, UnitPrice = 10m, TaxableKind = TaxableItemKind.Physical },
                new CheckoutLineItem { ItemId = "shipping", Quantity = 1, UnitPrice = 15m, TaxableKind = TaxableItemKind.Shipping },
            ],
        };

    private static CheckoutInvoiceBuilder CreateBuilder()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CheckoutInvoiceBuilder(
            siteService.Object,
            new DefaultCheckoutDiscountService([], NullLogger<DefaultCheckoutDiscountService>.Instance),
            new NullCheckoutTaxService());
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
