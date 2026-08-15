using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Moq;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutTaxServiceTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task ApplyTaxAsync_FoldsExclusiveTaxIntoInitialPaymentAndGrandTotal()
    {
        // Arrange: an exclusive (added-on-top) tax of $3 on a $30 one-time charge must both raise the grand
        // total and be folded into the up-front amount actually charged, so the tax is collected and not
        // just displayed.
        var taxService = new StubTaxService(new TaxCalculationResult
        {
            Currency = Currency,
            TaxAmount = 3m,
            Lines = [new TaxLine { TaxAmount = 3m, IncludedInPrice = false }],
        });

        var service = CreateService(taxService);

        var invoice = new CheckoutInvoice
        {
            Currency = Currency,
            InitialPaymentAmount = 30d,
            DueNow = 30d,
            LineItems = [new CheckoutLineItem { Id = "book", Quantity = 1, UnitPrice = 30d }],
        };

        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s1", Status = CheckoutSessionStatus.Pending });

        // Act
        await service.ApplyTaxAsync(invoice, flow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3d, invoice.TaxAmount);
        Assert.Equal(33d, invoice.GrandTotal);
        Assert.Equal(33d, invoice.InitialPaymentAmount);
        Assert.NotNull(invoice.TaxSnapshot);
    }

    [Fact]
    public async Task ApplyTaxAsync_DoesNotFoldInclusiveTaxIntoInitialPayment()
    {
        // Arrange: tax already included in the price must not be added on top of the up-front charge.
        var taxService = new StubTaxService(new TaxCalculationResult
        {
            Currency = Currency,
            TaxAmount = 5m,
            Lines = [new TaxLine { TaxAmount = 5m, IncludedInPrice = true }],
        });

        var service = CreateService(taxService);

        var invoice = new CheckoutInvoice
        {
            Currency = Currency,
            InitialPaymentAmount = 50d,
            DueNow = 50d,
            LineItems = [new CheckoutLineItem { Id = "book", Quantity = 1, UnitPrice = 50d }],
        };

        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s1", Status = CheckoutSessionStatus.Pending });

        // Act
        await service.ApplyTaxAsync(invoice, flow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(50d, invoice.GrandTotal);
        Assert.Equal(50d, invoice.InitialPaymentAmount);
    }

    [Fact]
    public async Task ApplyTaxAsync_WhenNoTaxableItems_SetsGrandTotalToDueNow()
    {
        // Arrange: an invoice with no due-now line items must not call the tax framework and must set the
        // grand total to the amount due now.
        var taxService = new StubTaxService(new TaxCalculationResult { Currency = Currency });
        var service = CreateService(taxService);

        var invoice = new CheckoutInvoice
        {
            Currency = Currency,
            DueNow = 0d,
            LineItems = [],
        };

        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s1", Status = CheckoutSessionStatus.Pending });

        // Act
        await service.ApplyTaxAsync(invoice, flow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0d, invoice.GrandTotal);
        Assert.Equal(0, taxService.CallCount);
        Assert.Null(invoice.TaxSnapshot);
    }

    [Fact]
    public async Task ApplyRecurringTaxAsync_ExtractsTaxFromChargedAmount()
    {
        // Arrange: the amount a provider charged for a cycle is tax-inclusive, so the recorded tax is the
        // extracted portion and a fresh snapshot is captured for the cycle.
        var taxService = new StubTaxService(new TaxCalculationResult
        {
            Currency = Currency,
            TaxAmount = 2m,
            Lines = [new TaxLine { TaxAmount = 2m, IncludedInPrice = true }],
        });

        var service = CreateService(taxService);

        var payment = new PaymentRecord { Amount = 22d, Currency = Currency };
        var session = new CheckoutSession { SessionId = "s1", Status = CheckoutSessionStatus.Pending };

        // Act
        await service.ApplyRecurringTaxAsync(payment, session, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2d, payment.TaxAmount);
        Assert.NotNull(payment.TaxSnapshot);
    }

    private static CheckoutTaxService CreateService(ITaxService taxService)
        => new(
            taxService,
            new StubSnapshotFactory(),
            new StubProfileProvider(),
            Mock.Of<IClock>());

    private sealed class StubTaxService : ITaxService
    {
        private readonly TaxCalculationResult _result;

        public StubTaxService(TaxCalculationResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<TaxCalculationResult> CalculateAsync(TaxCalculationContext context, CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(_result);
        }
    }

    private sealed class StubSnapshotFactory : ITaxSnapshotFactory
    {
        public TaxSnapshot Create(TaxCalculationContext context, TaxCalculationResult result)
            => new();
    }

    private sealed class StubProfileProvider : ICheckoutTaxProfileProvider
    {
        public Task<CheckoutTaxProfile> GetProfileAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckoutTaxProfile());

        public Task<CheckoutTaxProfile> GetProfileAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckoutTaxProfile());
    }
}
