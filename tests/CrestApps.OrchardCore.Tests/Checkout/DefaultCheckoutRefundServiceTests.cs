using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class DefaultCheckoutRefundServiceTests
{
    private const string SessionId = "session-1";
    private const string TransactionId = "pi_1";

    [Fact]
    public async Task RequestRefundAsync_FullRefund_RefundsGrossAndRecordsSucceeded()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        var service = CreateService(refundStore, provider, out _, new FakeTaxRefundCalculator());

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(110m, refund.RefundGrossAmount);
        Assert.Single(provider.Contexts);
        Assert.Equal(110m, provider.Contexts[0].Amount);
        Assert.Equal("re_1", refund.ProviderRefundReference);
    }

    [Fact]
    public async Task RequestRefundAsync_WhenAlreadyFullyRefunded_Throws()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        var service = CreateService(refundStore, provider, out _, new FakeTaxRefundCalculator());

        await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestRefundAsync(new RequestPaymentRefundContext
            {
                SessionId = SessionId,
                OriginalTransactionId = TransactionId,
                Amount = 1m,
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestRefundAsync_WhenAmountExceedsRemaining_Throws()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        var service = CreateService(refundStore, provider, out _, new FakeTaxRefundCalculator());

        await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
            Amount = 60m,
        }, TestContext.Current.CancellationToken);

        // Act & Assert: remaining is 50, so 60 must be rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestRefundAsync(new RequestPaymentRefundContext
            {
                SessionId = SessionId,
                OriginalTransactionId = TransactionId,
                Amount = 60m,
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestRefundAsync_WhenNoRefundProvider_RecordsPendingManualReview()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();

        // A calculator is present so tax can be allocated; the manual review here is caused purely by the
        // resolver having no provider matching the attempt's provider key.
        var calculator = new FakeTaxRefundCalculator();
        var service = CreateService(refundStore, refundProvider: null, out _, calculator);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, refund.Status);
        Assert.Equal(110m, refund.RefundGrossAmount);
        Assert.Equal(10m, refund.RefundTaxAmount);
    }

    [Fact]
    public async Task RequestRefundAsync_WhenTaxCollectedButNoCalculator_RecordsManualReviewAndDoesNotCallProvider()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        // The original payment collected tax (ConfirmedTaxAmount = 10) but no tax refund calculator is
        // registered, so the refunded tax cannot be allocated from the snapshot.
        var service = CreateService(refundStore, provider, out _, taxRefundCalculator: null);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, refund.Status);
        Assert.Equal("tax_allocation_unavailable", refund.FailureCode);
        Assert.Equal(0m, refund.RefundTaxAmount);
        Assert.Empty(provider.Contexts);
    }

    [Fact]
    public async Task RequestRefundAsync_WhenNoTaxCollectedAndNoCalculator_RefundsWithoutTax()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        // A payment that never collected tax has nothing to allocate, so a zero-tax refund is correct.
        var service = CreateService(refundStore, provider, out _, taxRefundCalculator: null, taxable: false);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(0m, refund.RefundTaxAmount);
        Assert.Single(provider.Contexts);
    }

    [Fact]
    public async Task RequestRefundAsync_FullRefund_AllocatesTaxFromSnapshot()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        var calculator = new FakeTaxRefundCalculator();
        var service = CreateService(refundStore, provider, out _, calculator);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(10m, refund.RefundTaxAmount);
        Assert.Equal(100m, refund.RefundTaxableAmount);
    }

    [Fact]
    public async Task RequestRefundAsync_WhenSnapshotTaxDisagreesWithConfirmedTax_RecordsManualReview()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var provider = new FakeCheckoutPaymentRefundProvider(
            "Stripe",
            ctx => PaymentRefundResult.Success("re_1", ctx.Amount, ctx.Currency, GatewayMode.Testing));

        // The snapshot claims 5 tax while the authoritative confirmed ledger recorded 10. The snapshot
        // must not be trusted to allocate, so the refund goes to manual review instead of allocating a
        // tax amount that was never confirmed as collected.
        var service = CreateService(refundStore, provider, out _, new FakeTaxRefundCalculator(), snapshotTaxAmount: 5m);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, refund.Status);
        Assert.Equal("tax_allocation_unavailable", refund.FailureCode);
        Assert.Equal(0m, refund.RefundTaxAmount);
        Assert.Empty(provider.Contexts);
    }

    private static DefaultCheckoutRefundService CreateService(
        InMemoryPaymentRefundStore refundStore,
        FakeCheckoutPaymentRefundProvider refundProvider,
        out InMemoryPaymentAttemptStore attemptStore,
        ITaxRefundCalculator taxRefundCalculator = null,
        bool taxable = true,
        decimal? snapshotTaxAmount = null)
    {
        var snapshot = taxable
            ? new TaxSnapshot
            {
                Currency = "usd",
                TaxableAmount = 100m,
                TaxAmount = snapshotTaxAmount ?? 10m,
                TotalAmount = 100m + (snapshotTaxAmount ?? 10m),
            }
            : null;

        var attempt = new PaymentAttempt
        {
            Id = "attempt-1",
            SessionId = SessionId,
            ProviderKey = "Stripe",
            State = PaymentAttemptState.Succeeded,
            TransactionId = TransactionId,
            ProviderReference = TransactionId,
            Currency = "usd",
            ConfirmedAmount = 100,
            ConfirmedTaxAmount = taxable ? 10 : 0,
            TaxSnapshot = snapshot,
            GatewayMode = GatewayMode.Testing,
        };

        attemptStore = new InMemoryPaymentAttemptStore(attempt);

        var sessionStore = new Mock<ICheckoutSessionStore>();
        sessionStore
            .Setup(s => s.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSession { SessionId = SessionId, Currency = "usd" });

        ICheckoutPaymentRefundProvider[] providers = refundProvider is null
            ? []
            : [refundProvider];

        var resolver = new CheckoutPaymentRefundProviderResolver(
            providers,
            NullLogger<CheckoutPaymentRefundProviderResolver>.Instance);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        ITaxRefundCalculator[] calculators = taxRefundCalculator is null
            ? []
            : [taxRefundCalculator];

        return new DefaultCheckoutRefundService(
            sessionStore.Object,
            attemptStore,
            refundStore,
            resolver,
            distributedLock.Object,
            calculators,
            Mock.Of<IClock>(),
            NullLogger<DefaultCheckoutRefundService>.Instance);
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeTaxRefundCalculator : ITaxRefundCalculator
    {
        public TaxRefundResult CalculateFullRefund(TaxSnapshot snapshot)
            => new()
            {
                RefundedTaxableAmount = snapshot.TaxableAmount,
                RefundedTaxAmount = snapshot.TaxAmount,
                RefundedTotalAmount = snapshot.TotalAmount,
            };

        public TaxRefundResult CalculateProportionalRefund(TaxSnapshot snapshot, decimal refundTotalAmount)
        {
            var ratio = refundTotalAmount / snapshot.TotalAmount;

            return new TaxRefundResult
            {
                RefundedTaxableAmount = snapshot.TaxableAmount * ratio,
                RefundedTaxAmount = snapshot.TaxAmount * ratio,
                RefundedTotalAmount = refundTotalAmount,
            };
        }
    }
}
