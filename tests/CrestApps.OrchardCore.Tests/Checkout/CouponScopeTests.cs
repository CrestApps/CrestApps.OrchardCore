using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A coupon meant for one family (financial aid) or one kind of charge must not work anywhere else, and a code on a
/// checkout that settles a transaction must be redeemed by whatever settles it, only once it is settled.
/// </summary>
public sealed class CouponScopeTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CouponScopedToASource_DiscountsOnlyAPaymentFromThatSource()
    {
        // Arrange
        var coupon = CreateCoupon("SPORTS10");
        coupon.Sources = ["registrations"];

        var provider = CreateProvider(coupon);

        // Act
        var forRegistration = await provider.GetDiscountsAsync(CreateContext(CreateSettlementSession("SPORTS10", source: "registrations")), TestContext.Current.CancellationToken);
        var forOtherSource = await provider.GetDiscountsAsync(CreateContext(CreateSettlementSession("SPORTS10", source: "pay-later")), TestContext.Current.CancellationToken);
        var forPurchase = await provider.GetDiscountsAsync(CreateContext(CreatePurchaseSession("SPORTS10")), TestContext.Current.CancellationToken);

        // Assert
        var discount = Assert.Single(forRegistration);
        Assert.Equal(10m, discount.Amount);
        Assert.Empty(forOtherSource);
        Assert.Empty(forPurchase);
    }

    [Fact]
    public async Task IsInScopeAsync_ForAnUnscopedCoupon_IsTrueEverywhere()
    {
        // Arrange
        var evaluator = new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider());
        var coupon = CreateCoupon("WELCOME");

        // Act & Assert
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(CreatePurchaseSession("WELCOME")), TestContext.Current.CancellationToken));
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(CreateSettlementSession("WELCOME", source: "registrations")), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsInScopeAsync_ForAReferenceType_MatchesTheCheckoutOrWhatTheTransactionIsFor()
    {
        // Arrange
        var evaluator = new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider());
        var forTransactions = CreateCoupon("PAYOFF");
        forTransactions.ReferenceTypes = ["transaction"];

        var forRegistrations = CreateCoupon("FALL");
        forRegistrations.ReferenceTypes = ["Registration"];

        var settlement = new CheckoutFlow(CreateSettlementSession("PAYOFF", source: "registrations", referenceType: "Registration"));
        var purchase = new CheckoutFlow(CreatePurchaseSession("PAYOFF"));

        // Act & Assert
        Assert.True(await evaluator.IsInScopeAsync(forTransactions, settlement, TestContext.Current.CancellationToken));
        Assert.True(await evaluator.IsInScopeAsync(forRegistrations, settlement, TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(forTransactions, purchase, TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(forRegistrations, purchase, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsInScopeAsync_ForOneCustomer_RefusesEveryoneElse()
    {
        // Arrange
        var evaluator = new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider());
        var coupon = CreateCoupon("AID-SMITH");
        coupon.CustomerId = "customer-1";

        var theirs = CreateSettlementSession("AID-SMITH", source: "registrations", customerId: "customer-1");
        var someoneElses = CreateSettlementSession("AID-SMITH", source: "registrations", customerId: "customer-2");
        var signedIn = CreatePurchaseSession("AID-SMITH");
        signedIn.OwnerId = "customer-1";

        // Act & Assert
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(theirs), TestContext.Current.CancellationToken));
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(signedIn), TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(someoneElses), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsInScopeAsync_ForOneEmail_MatchesTheCheckoutOrTheAccountWithoutCase()
    {
        // Arrange
        var contacts = new Mock<ICustomerContactResolver>();
        contacts
            .Setup(resolver => resolver.ResolveAsync(It.Is<CustomerOwner>(owner => owner.Id == "customer-1"), It.IsAny<ICustomerContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ICustomerContact>(contact => contact.Email == "Jane.Smith@Example.com"));

        var evaluator = new CouponScopeEvaluator(new ServiceCollection().AddSingleton(contacts.Object).BuildServiceProvider());
        var coupon = CreateCoupon("AID-SMITH");
        coupon.CustomerEmail = "jane.smith@example.com";

        var guest = CreatePurchaseSession("AID-SMITH");
        guest.Put(new CheckoutContactInfo { Email = "JANE.SMITH@example.com" });

        var account = CreatePurchaseSession("AID-SMITH");
        account.OwnerId = "customer-1";

        var stranger = CreatePurchaseSession("AID-SMITH");
        stranger.Put(new CheckoutContactInfo { Email = "someone@example.com" });

        // Act & Assert
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(guest), TestContext.Current.CancellationToken));
        Assert.True(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(account), TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(coupon, new CheckoutFlow(stranger), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplyDiscountsAsync_ForASettlementThatTakesDiscounts_AppliesThem()
    {
        // Arrange
        var coupon = CreateCoupon("FREE100");
        coupon.Percentage = 100m;

        var service = new DefaultCheckoutDiscountService([CreateProvider(coupon)], NullLogger<DefaultCheckoutDiscountService>.Instance);
        var invoice = CreateInvoice(100m);
        var flow = new CheckoutFlow(CreateSettlementSession("FREE100", source: "registrations"));

        // Act
        await service.ApplyDiscountsAsync(invoice, flow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, invoice.DueNow);
        Assert.Equal(100m, invoice.DiscountTotal);
    }

    [Fact]
    public async Task RedemptionHandler_LeavesASettlementCheckoutsCodeToWhatSettlesIt()
    {
        // Arrange
        var redeemed = 0;
        var provider = new Mock<ICheckoutDiscountProvider>();
        provider.SetupGet(candidate => candidate.Key).Returns(CouponDiscountProvider.ProviderKey);
        provider
            .Setup(candidate => candidate.RedeemAsync(It.IsAny<CheckoutDiscountRedemptionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => redeemed++)
            .Returns(Task.CompletedTask);

        var handler = new CouponRedemptionCheckoutHandler([provider.Object], Mock.Of<ICouponRedemptionService>(), NullLogger<CouponRedemptionCheckoutHandler>.Instance);

        var settlement = CreateSettlementSession("FREE100", source: "registrations");
        settlement.Put(CreateInvoiceWithDiscount("FREE100"));

        var purchase = CreatePurchaseSession("FREE100");
        purchase.Put(CreateInvoiceWithDiscount("FREE100"));

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(settlement)));
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(purchase)));

        // Assert: only the purchase redeemed its code at completion.
        Assert.Equal(1, redeemed);
    }

    private static Coupon CreateCoupon(string code)
        => new()
        {
            ItemId = code,
            Code = code,
            Kind = CouponKind.Percentage,
            Percentage = 10m,
            Target = DiscountTarget.OneTime,
            IsEnabled = true,
        };

    private static CouponDiscountProvider CreateProvider(Coupon coupon)
    {
        var store = new Mock<ICouponStore>();
        store
            .Setup(candidate => candidate.GetByCodeAsync(It.Is<string>(code => string.Equals(code, coupon.Code, StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        return new CouponDiscountProvider(
            store.Object,
            new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider()),
            Mock.Of<ICouponRedemptionService>(),
            new TestClock(_now));
    }

    private static CheckoutDiscountContext CreateContext(CheckoutSession session)
        => new(CreateInvoice(100m), new CheckoutFlow(session));

    private static CheckoutInvoice CreateInvoice(decimal amount)
        => new()
        {
            Currency = "USD",
            InitialPaymentAmount = amount,
            DueNow = amount,
            GrandTotal = amount,
            LineItems = [],
        };

    private static CheckoutInvoice CreateInvoiceWithDiscount(string code)
    {
        var invoice = CreateInvoice(0m);

        invoice.Discounts = [new DiscountLine { ProviderKey = CouponDiscountProvider.ProviderKey, Code = code, Amount = 100m, Target = DiscountTarget.OneTime }];

        return invoice;
    }

    // A checkout that pays a transaction, as the Transactions module starts it: it settles an existing balance, takes
    // discounts on it, and says what the transaction is.
    private static CheckoutSession CreateSettlementSession(string code, string source, string referenceType = null, string customerId = null)
    {
        var session = new CheckoutSession
        {
            SessionId = "settlement-1",
            ReferenceType = "Transaction",
            ReferenceId = "transaction-1",
            SettlesExistingBalance = true,
            AcceptsDiscountsOnBalance = true,
        };

        session.Put(new CheckoutCoupon { Code = code });
        session.Put(new CheckoutDiscountScope
        {
            Source = source,
            ReferenceType = referenceType,
            CustomerId = customerId,
        });

        return session;
    }

    private static CheckoutSession CreatePurchaseSession(string code)
    {
        var session = new CheckoutSession
        {
            SessionId = "purchase-1",
            ReferenceType = "Order",
            ReferenceId = "order-1",
        };

        session.Put(new CheckoutCoupon { Code = code });

        return session;
    }
}
