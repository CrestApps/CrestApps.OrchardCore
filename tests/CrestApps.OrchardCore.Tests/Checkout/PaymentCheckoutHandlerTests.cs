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
        var handler = CreateHandler(new StubReconciliationService());

        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems =
            [
                new BillingItem { Id = "book", Description = "Book", Amount = 30d },
                new BillingItem { Id = "plan", Description = "Membership", Amount = 10d, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
            ],
        });

        var flow = new CheckoutFlow(session);

        // Act
        await handler.ActivatedAsync(new CheckoutFlowActivatedContext(flow));

        // Assert
        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(Currency, invoice.Currency);
        Assert.Equal(30d, invoice.InitialPaymentAmount);
        Assert.Equal(10d, invoice.FirstRecurringPaymentAmount);
        Assert.Equal(40d, invoice.DueNow);
        Assert.Equal(2, invoice.LineItems.Length);

        // The grand total is the amount due now because the no-op tax service applies no tax.
        Assert.Equal(40d, invoice.GrandTotal);
    }

    [Fact]
    public async Task CompletingAsync_ReturnsQuietly_WhenAllObligationsSettle()
    {
        // Arrange
        var reconciliation = new StubReconciliationService
        {
            Result = new CheckoutReconciliationResult { IsFullySettled = true },
        };
        var handler = CreateHandler(reconciliation);

        var session = BuildSessionWithInvoice(30d);
        var flow = new CheckoutFlow(session);

        // Act & Assert: no exception means completion succeeded.
        await handler.CompletingAsync(new CheckoutFlowCompletingContext(flow));

        Assert.Equal(1, reconciliation.CallCount);
    }

    [Fact]
    public async Task CompletingAsync_Throws_WhenAnObligationFailsAtProvider()
    {
        // Arrange: a failed obligation must abort immediately so the checkout never reports a paid record
        // that the provider contradicts.
        var result = new CheckoutReconciliationResult { IsFullySettled = false };
        result.FailedObligationIds.Add(CheckoutObligations.OneTime);

        var reconciliation = new StubReconciliationService { Result = result };
        var handler = CreateHandler(reconciliation);

        var session = BuildSessionWithInvoice(30d);
        var flow = new CheckoutFlow(session);

        // Act & Assert
        await Assert.ThrowsAsync<CheckoutPaymentException>(
            () => handler.CompletingAsync(new CheckoutFlowCompletingContext(flow)));
    }

    [Fact]
    public async Task CompletingAsync_Throws_WhenNoInvoiceExists()
    {
        // Arrange
        var handler = CreateHandler(new StubReconciliationService());

        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
        var flow = new CheckoutFlow(session);

        // Act & Assert
        await Assert.ThrowsAsync<CheckoutPaymentException>(
            () => handler.CompletingAsync(new CheckoutFlowCompletingContext(flow)));
    }

    private static CheckoutSession BuildSessionWithInvoice(double oneTimeAmount)
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

    private static PaymentCheckoutHandler CreateHandler(ICheckoutReconciliationService reconciliationService)
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = Currency });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new PaymentCheckoutHandler(
            siteService.Object,
            new NoTaxCheckoutTaxService(),
            reconciliationService,
            CheckoutTestHelpers.CreatePaymentSessionCache(),
            NullLogger<PaymentCheckoutHandler>.Instance,
            Mock.Of<IStringLocalizer<PaymentCheckoutHandler>>());
    }

    private sealed class StubReconciliationService : ICheckoutReconciliationService
    {
        public CheckoutReconciliationResult Result { get; set; } = new() { IsFullySettled = true };

        public int CallCount { get; private set; }

        public Task<CheckoutReconciliationResult> ReconcileAsync(
            CheckoutSession session,
            IEnumerable<string> expectedObligationIds,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(Result);
        }
    }

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
