using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class PaymentCheckoutHandlerTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task ActivatedAsync_BuildsInvoiceFromBillingItems_SeparatingOneTimeAndRecurring()
    {
        // Arrange
        var handler = CreateHandler();

        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems =
            [
                new BillingItem { ItemId = "book", Description = "Book", Amount = 30m },
                new BillingItem { ItemId = "plan", Description = "Membership", Amount = 10m, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
            ],
        });

        var flow = new CheckoutFlow(session);

        // Act
        await handler.ActivatedAsync(new CheckoutFlowActivatedContext(flow));

        // Assert
        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(Currency, invoice.Currency);
        Assert.Equal(30m, invoice.InitialPaymentAmount);
        Assert.Equal(10m, invoice.FirstRecurringPaymentAmount);
        Assert.Equal(40m, invoice.DueNow);
        Assert.Equal(2, invoice.LineItems.Length);

        // The grand total is the amount due now because the no-op tax service applies no tax.
        Assert.Equal(40m, invoice.GrandTotal);
    }

    /// <summary>
    /// Settlement belongs to the engine, which confirms every obligation against the provider before any
    /// handler runs. What this handler still owns is the invariant that a completed checkout carries the
    /// invoice it was paid against, because receipts, tax records, and reporting are all built from it.
    /// </summary>
    [Fact]
    public async Task CompletingAsync_ReturnsQuietly_WhenTheSessionCarriesItsInvoice()
    {
        var handler = CreateHandler();
        var flow = new CheckoutFlow(BuildSessionWithInvoice(30m));

        var exception = await Record.ExceptionAsync(() => handler.CompletingAsync(new CheckoutFlowCompletingContext(flow)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CompletingAsync_Throws_WhenNoInvoiceExists()
    {
        var handler = CreateHandler();

        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
        var flow = new CheckoutFlow(session);

        await Assert.ThrowsAsync<CheckoutPaymentException>(
            () => handler.CompletingAsync(new CheckoutFlowCompletingContext(flow)));
    }

    private static CheckoutSession BuildSessionWithInvoice(decimal oneTimeAmount)
    {
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
        session.Put(new CheckoutInvoice
        {
            Currency = Currency,
            InitialPaymentAmount = oneTimeAmount,
            DueNow = oneTimeAmount,
            LineItems = [],
        });

        return session;
    }

    private static PaymentCheckoutHandler CreateHandler()
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = Currency });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new PaymentCheckoutHandler(
            siteService.Object,
            new DefaultCheckoutDiscountService([], NullLogger<DefaultCheckoutDiscountService>.Instance),
            new NoTaxCheckoutTaxService(),
            CheckoutTestHelpers.CreatePaymentSessionCache(),
            Mock.Of<IStringLocalizer<PaymentCheckoutHandler>>());
    }

    // A stand-in for the disabled Taxation feature: it leaves the invoice untaxed and sets the grand total to
    // the amount due now, which is exactly what the shipped no-op implementation does.
    private sealed class NoTaxCheckoutTaxService : ICheckoutTaxService
    {
        public Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
        {
            invoice.TaxAmount = 0m;
            invoice.GrandTotal = invoice.DueNow;

            return Task.CompletedTask;
        }

        public Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
