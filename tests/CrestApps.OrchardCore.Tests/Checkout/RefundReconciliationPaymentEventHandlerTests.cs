using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Moq;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class RefundReconciliationPaymentEventHandlerTests
{
    [Fact]
    public async Task PaymentRefundedAsync_WhenOriginalTransactionIdMissing_DoesNotReconcile()
    {
        // Arrange
        var reconciliation = new Mock<ICheckoutRefundReconciliationService>();
        var handler = new RefundReconciliationPaymentEventHandler(reconciliation.Object);

        // Act
        await handler.PaymentRefundedAsync(new PaymentRefundedContext
        {
            OriginalTransactionId = string.Empty,
            ProviderRefundReference = "re_1",
        });

        // Assert
        reconciliation.Verify(
            r => r.ReconcileRemoteRefundAsync(It.IsAny<ReconcileRemoteRefundContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PaymentRefundedAsync_MapsGatewayFieldsOntoReconciliationContext()
    {
        // Arrange
        ReconcileRemoteRefundContext captured = null;

        var reconciliation = new Mock<ICheckoutRefundReconciliationService>();
        reconciliation
            .Setup(r => r.ReconcileRemoteRefundAsync(It.IsAny<ReconcileRemoteRefundContext>(), It.IsAny<CancellationToken>()))
            .Callback<ReconcileRemoteRefundContext, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync(new PaymentRefund());

        var handler = new RefundReconciliationPaymentEventHandler(reconciliation.Object);

        // Act
        await handler.PaymentRefundedAsync(new PaymentRefundedContext
        {
            GatewayId = "Stripe",
            GatewayMode = GatewayMode.Live,
            OriginalTransactionId = "pi_1",
            ProviderRefundReference = "re_1",
            RefundedAmount = 42m,
            Currency = "usd",
            RefundStatus = "succeeded",
            Reason = "requested_by_customer",
            IdempotencyKey = "idem-1",
        });

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("pi_1", captured.OriginalTransactionId);
        Assert.Equal("re_1", captured.ProviderRefundReference);
        Assert.Equal("Stripe", captured.ProviderKey);
        Assert.Equal(42m, captured.RefundedAmount);
        Assert.Equal("usd", captured.Currency);
        Assert.Equal(RefundStatus.Succeeded, captured.Status);
        Assert.Equal("requested_by_customer", captured.Reason);
        Assert.Equal("idem-1", captured.IdempotencyKey);
        Assert.Equal(GatewayMode.Live, captured.GatewayMode);
    }

    [Theory]
    [InlineData("", RefundStatus.Pending)]
    [InlineData("succeeded", RefundStatus.Succeeded)]
    [InlineData("COMPLETE", RefundStatus.Succeeded)]
    [InlineData("failed", RefundStatus.Failed)]
    [InlineData("cancelled", RefundStatus.Canceled)]
    [InlineData("pending", RefundStatus.Pending)]
    [InlineData("something-unknown", RefundStatus.Pending)]
    public async Task PaymentRefundedAsync_MapsGatewayStatusStringToRefundStatus(string gatewayStatus, RefundStatus expected)
    {
        // Arrange
        ReconcileRemoteRefundContext captured = null;

        var reconciliation = new Mock<ICheckoutRefundReconciliationService>();
        reconciliation
            .Setup(r => r.ReconcileRemoteRefundAsync(It.IsAny<ReconcileRemoteRefundContext>(), It.IsAny<CancellationToken>()))
            .Callback<ReconcileRemoteRefundContext, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync(new PaymentRefund());

        var handler = new RefundReconciliationPaymentEventHandler(reconciliation.Object);

        // Act
        await handler.PaymentRefundedAsync(new PaymentRefundedContext
        {
            OriginalTransactionId = "pi_1",
            RefundStatus = gatewayStatus,
        });

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expected, captured.Status);
    }
}
