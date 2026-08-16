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

        var service = CreateService(refundStore, provider, out _);

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

        var service = CreateService(refundStore, provider, out _);

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

        var service = CreateService(refundStore, provider, out _);

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

        // The resolver has no provider matching the attempt's provider key.
        var service = CreateService(refundStore, refundProvider: null, out _);

        // Act
        var refund = await service.RequestRefundAsync(new RequestPaymentRefundContext
        {
            SessionId = SessionId,
            OriginalTransactionId = TransactionId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, refund.Status);
        Assert.Equal(110m, refund.RefundGrossAmount);
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
        Assert.Equal(10m, refund.RefundTaxAmount);
        Assert.Equal(100m, refund.RefundTaxableAmount);
    }

    private static DefaultCheckoutRefundService CreateService(
        InMemoryPaymentRefundStore refundStore,
        FakeCheckoutPaymentRefundProvider refundProvider,
        out InMemoryPaymentAttemptStore attemptStore,
        ITaxRefundCalculator taxRefundCalculator = null)
    {
        var snapshot = new TaxSnapshot
        {
            Currency = "usd",
            TaxableAmount = 100m,
            TaxAmount = 10m,
            TotalAmount = 110m,
        };

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
            ConfirmedTaxAmount = 10,
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
