using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class DefaultCheckoutRefundReconciliationServiceTests
{
    private const string TransactionId = "pi_1";
    private const string Currency = "usd";

    [Fact]
    public async Task ReconcileRemoteRefundAsync_WhenOriginalTransactionIdMissing_Throws()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var service = CreateService(refundStore);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
            {
                OriginalTransactionId = string.Empty,
                ProviderRefundReference = "re_1",
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_CorrelatesByProviderReference_AndAdvancesToSucceeded()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            Currency = Currency,
            RefundGrossAmount = 110m,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            RefundedAmount = 110m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("refund-1", result.ItemId);
        Assert.Equal(RefundStatus.Succeeded, result.Status);
        Assert.NotNull(result.CompletedUtc);
        Assert.Single((await refundStore.GetAllAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_DoesNotCorrelateProviderReference_AcrossDifferentTransaction()
    {
        // Arrange - a local refund carries a provider reference for one transaction.
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_shared",
            ProviderKey = "Stripe",
            Currency = Currency,
            RefundGrossAmount = 110m,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act - a remote refund reuses the same reference but reports a different original transaction.
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = "pi_other",
            ProviderRefundReference = "re_shared",
            ProviderKey = "Stripe",
            RefundedAmount = 110m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert - the cross-transaction match is refused; the unrelated local record is untouched and the
        // remote refund is quarantined as its own record.
        Assert.NotEqual("refund-1", result.ItemId);
        Assert.Equal(RefundStatus.PendingManualReview, result.Status);

        var original = Assert.Single(await refundStore.GetByOriginalTransactionAsync(TransactionId, TestContext.Current.CancellationToken));
        Assert.Equal("refund-1", original.ItemId);
        Assert.Equal(RefundStatus.Pending, original.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_CorrelatesByIdempotencyKey_AndAdoptsProviderReference()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            IdempotencyKey = "idem-1",
            Currency = Currency,
            RefundGrossAmount = 50m,
            Status = RefundStatus.Requested,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            IdempotencyKey = "idem-1",
            ProviderRefundReference = "re_1",
            RefundedAmount = 50m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("refund-1", result.ItemId);
        Assert.Equal("re_1", result.ProviderRefundReference);
        Assert.Equal(RefundStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_CorrelatesByOpenRequestAmount_WhenNoReferenceOrKey()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            Currency = Currency,
            RefundGrossAmount = 25m,
            Status = RefundStatus.Requested,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_9",
            RefundedAmount = 25m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("refund-1", result.ItemId);
        Assert.Equal("re_9", result.ProviderRefundReference);
        Assert.Equal(RefundStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_WhenNoLocalRequest_QuarantinesForManualReview()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_dashboard",
            ProviderKey = "Stripe",
            RefundedAmount = 40m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, result.Status);
        Assert.Equal("remote_refund_without_local_request", result.FailureCode);
        Assert.Equal("re_dashboard", result.ProviderRefundReference);
        Assert.Equal(40m, result.RefundGrossAmount);
        Assert.Equal(0m, result.RefundTaxAmount);
        Assert.False(string.IsNullOrEmpty(result.ItemId));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_DoesNotRegressManualReviewRecord()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            Currency = Currency,
            RefundGrossAmount = 10m,
            Status = RefundStatus.PendingManualReview,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            RefundedAmount = 10m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, result.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_IsIdempotentByProviderReference()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var service = CreateService(refundStore);

        var context = new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_dup",
            ProviderKey = "Stripe",
            RefundedAmount = 15m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
        };

        // Act
        var first = await service.ReconcileRemoteRefundAsync(context, TestContext.Current.CancellationToken);
        var second = await service.ReconcileRemoteRefundAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.ItemId, second.ItemId);
        Assert.Single((await refundStore.GetAllAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_DoesNotRegressTerminalStatus_OnStaleEvent()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            Currency = Currency,
            RefundGrossAmount = 10m,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            RefundedAmount = 10m,
            Currency = Currency,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_DoesNotFlipTerminalStatus_OnStaleTerminalEvent()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            Currency = Currency,
            RefundGrossAmount = 10m,
            Status = RefundStatus.Succeeded,
            CompletedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act - a stale, out-of-order terminal event reports the opposite terminal result.
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_1",
            RefundedAmount = 10m,
            Currency = Currency,
            Status = RefundStatus.Failed,
        }, TestContext.Current.CancellationToken);

        // Assert - the gateway-confirmed terminal result is immutable and its timestamp is preserved.
        Assert.Equal(RefundStatus.Succeeded, result.Status);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.CompletedUtc);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_RefreshesAggregateAmount_WithoutDoubleCounting()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var service = CreateService(refundStore);

        // Act - two aggregate deliveries report the charge's cumulative refunded total (10, then 30).
        var first = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderKey = "Stripe",
            RefundedAmount = 10m,
            Currency = Currency,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        var second = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderKey = "Stripe",
            RefundedAmount = 30m,
            Currency = Currency,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        // Assert - one record, refreshed to the latest cumulative total, never summed to 40.
        Assert.Equal(first.ItemId, second.ItemId);
        Assert.Equal(30m, second.RefundGrossAmount);
        Assert.Single(await refundStore.GetAllAsync(TestContext.Current.CancellationToken));

        // Act - an out-of-order redelivery of an older, smaller cumulative total arrives after the newer one.
        var stale = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderKey = "Stripe",
            RefundedAmount = 10m,
            Currency = Currency,
            Status = RefundStatus.Pending,
        }, TestContext.Current.CancellationToken);

        // Assert - the cumulative total only ever grows, so the stale delivery never regresses it below 30.
        Assert.Equal(first.ItemId, stale.ItemId);
        Assert.Equal(30m, stale.RefundGrossAmount);
        Assert.Single(await refundStore.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_QuarantinesAggregate_IsIdempotentAcrossRedeliveries()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        var service = CreateService(refundStore);

        // An aggregate notification carries neither a provider reference nor an idempotency key.
        var context = new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderKey = "Stripe",
            RefundedAmount = 30m,
            Currency = Currency,
            Status = RefundStatus.Pending,
        };

        // Act
        var first = await service.ReconcileRemoteRefundAsync(context, TestContext.Current.CancellationToken);
        var second = await service.ReconcileRemoteRefundAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, first.Status);
        Assert.Equal(first.ItemId, second.ItemId);
        Assert.Single((await refundStore.GetAllAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_CorrelatesByMetadataIdempotencyKey()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = TransactionId,
            IdempotencyKey = "idem-meta",
            Currency = Currency,
            RefundGrossAmount = 12m,
            Status = RefundStatus.Requested,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // Act
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderRefundReference = "re_meta",
            RefundedAmount = 12m,
            Currency = Currency,
            Status = RefundStatus.Succeeded,
            Metadata = new Dictionary<string, string>
            {
                [CheckoutRefundMetadataKeys.IdempotencyKey] = "idem-meta",
            },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("refund-1", result.ItemId);
        Assert.Equal("re_meta", result.ProviderRefundReference);
        Assert.Equal(RefundStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ReconcileRemoteRefundAsync_DoesNotMatchOpenRequestInDifferentCurrency()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();
        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-jpy",
            OriginalTransactionId = TransactionId,
            Currency = "jpy",
            RefundGrossAmount = 100m,
            Status = RefundStatus.Requested,
        }, TestContext.Current.CancellationToken);

        var service = CreateService(refundStore);

        // 1.00 USD and 100 JPY both scale to 100 minor units, but the currencies differ.
        var result = await service.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = TransactionId,
            ProviderKey = "Stripe",
            RefundedAmount = 1.00m,
            Currency = "usd",
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.PendingManualReview, result.Status);
        Assert.Equal(2, (await refundStore.GetAllAsync(TestContext.Current.CancellationToken)).Count);
    }

    private static DefaultCheckoutRefundReconciliationService CreateService(InMemoryPaymentRefundStore refundStore)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        return new DefaultCheckoutRefundReconciliationService(
            refundStore,
            distributedLock.Object,
            clock.Object,
            NullLogger<DefaultCheckoutRefundReconciliationService>.Instance);
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
