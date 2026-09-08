using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Pins what the invoice must include and what currency it is built in.
/// </summary>
/// <remarks>
/// Both cases here are defects that shipped and were only found by opening the page: a checkout whose whole
/// price sat on a concealed step totalled zero, and an invoice built in the site's currency from line items
/// priced in another would have charged a number belonging to a different currency.
/// </remarks>
public sealed class CheckoutInvoiceBuilderTests
{
    private const string SiteCurrency = "USD";

    /// <summary>
    /// A step is concealed because it has nothing to ask the customer, not because it has nothing to charge.
    /// The plan a subscriber already chose is exactly such a step, so leaving concealed steps out of the
    /// invoice completes the checkout for nothing.
    /// </summary>
    [Fact]
    public async Task BuildAsync_IncludesBillingItemsFromConcealedSteps()
    {
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "SubscriptionPlan",
            Order = 0,
            Conceal = true,
            BillingItems =
            [
                new BillingItem { ItemId = "plan", Description = "Membership", Amount = 25m, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
                new BillingItem { ItemId = "setup", Description = "Setup fee", Amount = 10m },
            ],
        });

        var flow = new CheckoutFlow(session);

        await CreateBuilder().BuildAsync(flow);

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(10m, invoice.InitialPaymentAmount);
        Assert.Equal(25m, invoice.FirstRecurringPaymentAmount);
        Assert.Equal(35m, invoice.DueNow);
        Assert.Equal(2, invoice.LineItems.Length);
    }

    /// <summary>
    /// What is being bought decides the currency. A plan priced in euros must not be invoiced in the site's
    /// dollars, because the amount would be charged as a number in the wrong currency.
    /// </summary>
    [Fact]
    public async Task BuildAsync_UsesTheSessionCurrency_WhenTheCheckoutNamesOne()
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            Status = CheckoutSessionStatus.Pending,
            Currency = "EUR",
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems = [new BillingItem { ItemId = "book", Description = "Book", Amount = 30m }],
        });

        await CreateBuilder().BuildAsync(new CheckoutFlow(session));

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal("EUR", invoice.Currency);
    }

    /// <summary>
    /// A checkout that never names a currency still has to be priced, so the site setting is the fallback.
    /// </summary>
    [Fact]
    public async Task BuildAsync_FallsBackToTheSiteCurrency_WhenTheCheckoutNamesNone()
    {
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems = [new BillingItem { ItemId = "book", Description = "Book", Amount = 30m }],
        });

        await CreateBuilder().BuildAsync(new CheckoutFlow(session));

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(SiteCurrency, invoice.Currency);
    }

    /// <summary>
    /// Rebuilding must replace the invoice rather than add to it, so applying and removing a coupon cannot
    /// leave the total drifting away from the line items the customer is reading.
    /// </summary>
    [Fact]
    public async Task BuildAsync_IsRepeatable()
    {
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems = [new BillingItem { ItemId = "book", Description = "Book", Amount = 30m }],
        });

        var flow = new CheckoutFlow(session);
        var builder = CreateBuilder();

        await builder.BuildAsync(flow);
        await builder.BuildAsync(flow);
        await builder.BuildAsync(flow);

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(30m, invoice.DueNow);
        Assert.Single(invoice.LineItems);
    }

    private static CheckoutInvoiceBuilder CreateBuilder()
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = SiteCurrency });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CheckoutInvoiceBuilder(
            siteService.Object,
            new DefaultCheckoutDiscountService([], NullLogger<DefaultCheckoutDiscountService>.Instance),
            new NoTaxCheckoutTaxService());
    }

    // Stands in for the disabled Taxation feature: it leaves the invoice untaxed and sets the grand total to
    // the amount due now, which is what the shipped no-op implementation does.
    private sealed class NoTaxCheckoutTaxService : ICheckoutTaxService
    {
        public Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
        {
            invoice.GrandTotal = invoice.DueNow;

            return Task.CompletedTask;
        }

        public Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
