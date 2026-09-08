using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// This handler is where a plan becomes money: it decides what the customer is charged, how often, for how
/// many cycles, and what one-time fee comes with it.
/// </summary>
/// <remarks>
/// A product may be offered on several sets of terms, and the wrong branch here is silent — the checkout
/// completes, the invoice looks plausible, and the customer is simply billed something they did not choose.
/// That is exactly what happened when the buyer's choice was applied to the session too late, so the rules
/// are pinned here rather than left to a live purchase to catch.
/// </remarks>
public sealed class SubscriptionPlanCheckoutHandlerTests
{
    /// <summary>
    /// The price the buyer chose decides the amount and the schedule.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_ChargesTheChosenPrice()
    {
        var price = Price("annual", 100m, BillingInterval.Year);
        var session = CreateSession(new CheckoutPriceSelection { PriceId = "annual" });

        await Activate(session, price);

        var step = Assert.Single(session.Steps);
        var line = Assert.Single(step.BillingItems);

        Assert.Equal(100m, line.Amount);
        Assert.Equal(DurationType.Year, line.Plan.DurationType);
        Assert.Equal(1, line.Plan.BillingDuration);
        Assert.Contains("Annual", line.Description);
    }

    /// <summary>
    /// The catalog keeps its own interval vocabulary, so the mapping to the billing one has to be right in
    /// every direction — a yearly plan billed monthly overcharges by twelve.
    /// </summary>
    [Theory]
    [InlineData(BillingInterval.Day, DurationType.Day)]
    [InlineData(BillingInterval.Week, DurationType.Week)]
    [InlineData(BillingInterval.Month, DurationType.Month)]
    [InlineData(BillingInterval.Year, DurationType.Year)]
    public async Task ActivatingAsync_MapsEveryInterval(BillingInterval interval, DurationType expected)
    {
        var session = CreateSession(new CheckoutPriceSelection { PriceId = "p" });

        await Activate(session, Price("p", 10m, interval));

        Assert.Equal(expected, Assert.Single(Assert.Single(session.Steps).BillingItems).Plan.DurationType);
    }

    /// <summary>
    /// A quantity bills for what the buyer took, every cycle — not for one of them.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_WithAQuantity_BillsTheSubtotal()
    {
        var price = Price("seats", 10m, BillingInterval.Month);
        var session = CreateSession(new CheckoutPriceSelection { PriceId = "seats", Quantity = 5 });

        await Activate(session, price, quantity: 5);

        Assert.Equal(50m, Assert.Single(Assert.Single(session.Steps).BillingItems).Amount);
    }

    /// <summary>
    /// The price's own setup fee is charged once alongside the first cycle, as its own line.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_WithASetupFee_AddsAOneTimeLine()
    {
        var price = Price("monthly", 10m, BillingInterval.Month);
        price.SetupFee = 50m;
        price.SetupFeeDescription = "Onboarding";

        var session = CreateSession(new CheckoutPriceSelection { PriceId = "monthly" });

        await Activate(session, price);

        var lines = Assert.Single(session.Steps).BillingItems.ToArray();

        Assert.Equal(2, lines.Length);

        var fee = lines.Single(line => line.Plan is null);

        Assert.Equal(50m, fee.Amount);
        Assert.Equal("Onboarding", fee.Description);
    }

    /// <summary>
    /// Trials and cycle limits ride on the chosen price, so a plan sold as "three cycles after a week free"
    /// reaches the gateway as exactly that.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_CarriesTheTrialAndCycleLimit()
    {
        var price = Price("intro", 10m, BillingInterval.Month);
        price.TrialDays = 7;
        price.BillingCycleLimit = 3;
        price.StartDayDelay = 2;

        var session = CreateSession(new CheckoutPriceSelection { PriceId = "intro" });

        await Activate(session, price);

        var plan = Assert.Single(Assert.Single(session.Steps).BillingItems).Plan;

        Assert.Equal(7, plan.TrialDays);
        Assert.Equal(3, plan.BillingCycleLimit);
        Assert.Equal(2, plan.StartDayDelay);
    }

    /// <summary>
    /// Only a fixed price names a reusable offer at the gateway. An amount the buyer chose has none, and
    /// passing one would reuse a price object for an amount nobody else will ever pay.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_ForABuyerNamedAmount_NamesNoReusableOffer()
    {
        var price = Price("supporter", 25m, BillingInterval.Month);
        price.AllowCustomAmount = true;

        var session = CreateSession(new CheckoutPriceSelection { PriceId = "supporter", CustomAmount = 42m });

        await Activate(session, price, unitPrice: 42m);

        var line = Assert.Single(Assert.Single(session.Steps).BillingItems);

        Assert.Equal(42m, line.Amount);
        Assert.Null(line.PriceId);
    }

    /// <summary>
    /// A fixed price passes its identifier through, which is what lets the gateway reuse one price for the
    /// offer instead of minting one per customer.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_ForAFixedPrice_NamesTheOffer()
    {
        var session = CreateSession(new CheckoutPriceSelection { PriceId = "monthly" });

        await Activate(session, Price("monthly", 10m, BillingInterval.Month));

        Assert.Equal("monthly", Assert.Single(Assert.Single(session.Steps).BillingItems).PriceId);
    }

    /// <summary>
    /// A plan that lists no prices is still sold on the terms of its subscription part, so adding prices
    /// stays opt-in and no existing plan had to be migrated to keep working.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_WithoutAChosenPrice_FallsBackToThePlanPart()
    {
        var session = CreateSession(selection: null);

        await Activate(session, price: null, unitPrice: 20m);

        var line = Assert.Single(Assert.Single(session.Steps).BillingItems);

        Assert.Equal(20m, line.Amount);
        Assert.Equal(DurationType.Month, line.Plan.DurationType);
        Assert.Equal(3, line.Plan.BillingDuration);

        // The plan part's own setup fee still applies when no price supplies one.
        Assert.Null(line.PriceId);
    }

    /// <summary>
    /// A one-time price cannot be sold as a subscription. Billing it as one would create an agreement that
    /// charges forever for something sold once.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_ForAOneTimePrice_AddsNothing()
    {
        var price = Price("once", 10m, BillingInterval.Month);
        price.Kind = PriceKind.OneTime;

        var session = CreateSession(new CheckoutPriceSelection { PriceId = "once" });

        await Activate(session, price);

        Assert.Empty(session.Steps);
    }

    /// <summary>
    /// A plan that cannot be priced at all is not turned into a free purchase.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_WhenThePriceIsRefused_AddsNothing()
    {
        var session = CreateSession(new CheckoutPriceSelection { PriceId = "withdrawn" });

        await Activate(session, price: null, refuse: true);

        Assert.Empty(session.Steps);
    }

    private static async Task Activate(
        CheckoutSession session,
        ProductPrice price,
        decimal? unitPrice = null,
        int quantity = 1,
        bool refuse = false)
    {
        var contentItem = new ContentItem
        {
            ContentType = "MembershipPlan",
            ContentItemId = "plan-1",
            ContentItemVersionId = "plan-1-v1",
            DisplayText = "Membership",
        };

        contentItem.Apply(nameof(SubscriptionPart), new SubscriptionPart
        {
            BillingDuration = 3,
            DurationType = DurationType.Month,
        });

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(m => m.GetAsync("plan-1", It.IsAny<VersionOptions>())).ReturnsAsync(contentItem);
        contentManager.Setup(m => m.GetAsync("plan-1")).ReturnsAsync(contentItem);

        var resolver = new Mock<IPriceResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refuse
                ? null
                : new PriceResult(unitPrice ?? price?.Amount ?? 20m, "USD", quantity, price));

        var handler = new SubscriptionPlanCheckoutHandler(
            contentManager.Object,
            resolver.Object,
            NullLogger<SubscriptionPlanCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<SubscriptionPlanCheckoutHandler>());

        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));
    }

    private static CheckoutSession CreateSession(CheckoutPriceSelection selection)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            ReferenceType = SubscriptionCheckout.ReferenceType,
            ReferenceId = "plan-1",
        };

        if (selection is not null)
        {
            session.Put(selection);
        }

        return session;
    }

    private static ProductPrice Price(string id, decimal amount, BillingInterval interval)
        => new()
        {
            PriceId = id,
            Name = char.ToUpperInvariant(id[0]) + id[1..],
            Currency = "USD",
            Amount = amount,
            Kind = PriceKind.Recurring,
            BillingDuration = 1,
            Interval = interval,
            IsActive = true,
        };
}
