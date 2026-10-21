using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Recipes;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Recipes.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Free shipping, maximum amounts, coupons for some products or categories, the payment gate that holds a use, and
/// the recipe step.
/// </summary>
public sealed class CouponKindsAndLimitsTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AFreeShippingCoupon_TakesOffTheShipping_AndLeavesTheItems()
    {
        var invoice = Invoice();

        await ApplyAsync(invoice, new Coupon { Code = "SHIPFREE", Kind = CouponKind.FreeShipping });

        var discount = Assert.Single(invoice.Discounts);
        Assert.Equal(DiscountTarget.Shipping, discount.Target);
        Assert.Equal(15m, discount.Amount);
        Assert.Equal(50m, invoice.DueNow);
        Assert.Equal([0m, 0m, 15m], invoice.LineItems.Select(line => line.DiscountAmount));
    }

    [Fact]
    public async Task AFreeShippingCoupon_CountsOnlyTheItems_TowardItsMinimum()
    {
        var invoice = Invoice();

        // $50 of items and $15 of shipping don't reach $60 of items.
        await ApplyAsync(invoice, new Coupon { Code = "SHIPFREE", Kind = CouponKind.FreeShipping, MinimumAmount = 60m });

        Assert.Empty(invoice.Discounts);
        Assert.Equal(65m, invoice.DueNow);
    }

    [Fact]
    public async Task ACouponForSomeProducts_TakesItsShareOffThoseProductsOnly()
    {
        var invoice = Invoice();

        await ApplyAsync(invoice, new Coupon { Code = "MUGS10", Kind = CouponKind.Percentage, Percentage = 10m, ProductIds = ["product-mug"] });

        Assert.Equal(3m, Assert.Single(invoice.Discounts).Amount);
        Assert.Equal([3m, 0m, 0m], invoice.LineItems.Select(line => line.DiscountAmount));
        Assert.Equal(62m, invoice.DueNow);
    }

    [Fact]
    public async Task ACouponForSomeCategories_FindsTheProductsInThem()
    {
        var invoice = Invoice();

        await ApplyAsync(invoice, new Coupon { Code = "HATS", Kind = CouponKind.FixedAmount, Amount = 5m, Currency = "USD", CategoryIds = ["term-hats"] });

        Assert.Equal(5m, Assert.Single(invoice.Discounts).Amount);
        Assert.Equal([0m, 5m, 0m], invoice.LineItems.Select(line => line.DiscountAmount));
    }

    [Fact]
    public async Task ACouponForProductsNotInTheOrder_TakesNothing()
    {
        var invoice = Invoice();

        await ApplyAsync(invoice, new Coupon { Code = "OTHER", Kind = CouponKind.Percentage, Percentage = 50m, ProductIds = ["product-elsewhere"] });

        Assert.Empty(invoice.Discounts);
        Assert.Equal(65m, invoice.DueNow);
    }

    [Fact]
    public async Task ACouponWithAMaximumAmount_DoesNothingAboveIt()
    {
        var small = Invoice();
        var large = Invoice();

        await ApplyAsync(small, new Coupon { Code = "SMALL", Kind = CouponKind.Percentage, Percentage = 10m, MaximumAmount = 50m });
        await ApplyAsync(large, new Coupon { Code = "SMALL", Kind = CouponKind.Percentage, Percentage = 10m, MaximumAmount = 49.99m });

        Assert.Equal(5m, Assert.Single(small.Discounts).Amount);
        Assert.Empty(large.Discounts);
    }

    [Fact]
    public async Task ThePaymentGate_TakesOffACodeThatRanOut_AndRefusesThePayment()
    {
        var redemptions = new Mock<ICouponRedemptionService>();
        redemptions
            .Setup(service => service.HoldAsync(It.IsAny<CheckoutFlow>(), It.IsAny<DiscountLine>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CouponAvailability.UsedUp);

        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutCoupon { Code = "LAST1" });
        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            Discounts = [new DiscountLine { ProviderKey = CouponDiscountProvider.ProviderKey, Code = "LAST1", Amount = 5m }],
        });

        var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(session), "test");

        await new CouponHoldPaymentGate(redemptions.Object, CreateBuilder(), new PassThroughLocalizer<CouponHoldPaymentGate>())
            .BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
        Assert.True(context.InvoiceChanged);
        Assert.Contains("LAST1", context.ErrorMessage, StringComparison.Ordinal);
        Assert.False(session.TryGet<CheckoutCoupon>(out _));
        Assert.True(session.TryGet<CheckoutInvoice>(out var rebuilt));
        Assert.Empty(rebuilt.Discounts ?? []);
    }

    [Fact]
    public async Task ThePaymentGate_HoldsEachCoupon_AndLetsThePaymentBegin()
    {
        var redemptions = new Mock<ICouponRedemptionService>();
        redemptions
            .Setup(service => service.HoldAsync(It.IsAny<CheckoutFlow>(), It.IsAny<DiscountLine>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CouponAvailability.Available);

        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            Discounts = [new DiscountLine { ProviderKey = CouponDiscountProvider.ProviderKey, Code = "SAVE10", Amount = 5m }],
        });

        var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(session), "test");

        await new CouponHoldPaymentGate(redemptions.Object, CreateBuilder(), new PassThroughLocalizer<CouponHoldPaymentGate>())
            .BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.IsRefused);
        redemptions.Verify(service => service.HoldAsync(It.IsAny<CheckoutFlow>(), It.Is<DiscountLine>(line => line.Code == "SAVE10"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheCouponsRecipeStep_AddsAndUpdatesByCode_WithoutTouchingTheUses()
    {
        var existing = new Coupon { ItemId = "c1", Code = "SAVE10", Kind = CouponKind.Percentage, Percentage = 10m, RedemptionCount = 7, IsEnabled = true };
        var created = new List<Coupon>();

        var store = new Mock<ICouponStore>();
        store.Setup(value => value.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        store.Setup(value => value.GetByCodeAsync("SHIPFREE", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
        store.Setup(value => value.CreateAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>())).Callback<Coupon, CancellationToken>((coupon, _) => created.Add(coupon)).Returns(ValueTask.CompletedTask);

        var step = new JsonObject
        {
            ["name"] = CheckoutConstants.CouponsRecipeStep,
            ["Coupons"] = new JsonArray
            {
                new JsonObject { ["Code"] = "SAVE10", ["Kind"] = "Percentage", ["Percentage"] = 15, ["RedemptionCount"] = 0, ["MaxRedemptionsPerCustomer"] = 1, ["IsEnabled"] = true },
                new JsonObject { ["Code"] = "SHIPFREE", ["Kind"] = "FreeShipping", ["MinimumAmount"] = 75, ["IsEnabled"] = true },
                new JsonObject { ["Code"] = "BROKEN", ["Kind"] = "FixedAmount", ["Amount"] = 5 },
            },
        };

        var context = new RecipeExecutionContext { Name = CheckoutConstants.CouponsRecipeStep, Step = step };

        await new CouponsStep(store.Object).ExecuteAsync(context);

        Assert.Equal(15m, existing.Percentage);
        Assert.Equal(1, existing.MaxRedemptionsPerCustomer);
        Assert.Equal(7, existing.RedemptionCount);

        var shipping = Assert.Single(created);
        Assert.Equal(CouponKind.FreeShipping, shipping.Kind);
        Assert.Equal(DiscountTarget.Shipping, shipping.EffectiveTarget);
        Assert.Equal(75m, shipping.MinimumAmount);

        // A fixed amount with no currency is refused, and said so.
        Assert.Single(context.Errors);
    }

    private static async Task ApplyAsync(CheckoutInvoice invoice, Coupon coupon)
    {
        coupon.ItemId ??= coupon.Code;
        coupon.IsEnabled = true;

        var store = new Mock<ICouponStore>();
        store.Setup(value => value.GetByCodeAsync(coupon.Code, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

        var provider = new CouponDiscountProvider(
            store.Object,
            new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider()),
            Mock.Of<ICouponRedemptionService>(),
            new TestClock(_now));

        var session = new CheckoutSession { SessionId = "session-1", Currency = "USD" };
        session.Put(new CheckoutCoupon { Code = coupon.Code });

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
