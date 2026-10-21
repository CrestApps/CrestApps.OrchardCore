using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// What an order gave is given when it is paid, and taken back when it is canceled or refunded in full; a partial
/// refund leaves it with the buyer.
/// </summary>
public sealed class OrderGrantHandlerBaseTests
{
    [Fact]
    public async Task APaidOrder_IsGranted()
    {
        // Arrange
        var handler = new RecordingGrantHandler();
        var order = new Order { ItemId = "order-1" };

        // Act
        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(order, Assert.Single(handler.Granted));
        Assert.Empty(handler.Revoked);
    }

    [Fact]
    public async Task ACanceledOrder_IsRevoked()
    {
        // Arrange
        var handler = new RecordingGrantHandler();
        var order = new Order { ItemId = "order-1" };

        // Act
        await handler.CanceledAsync(new OrderCanceledContext(order, OrderCancelReason.CheckoutCanceled), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OrderRevokeReason.Canceled, Assert.Single(handler.Revoked).Reason);
    }

    [Fact]
    public async Task OnlyAFullRefund_IsRevoked()
    {
        // Arrange
        var handler = new RecordingGrantHandler();
        var order = new Order { ItemId = "order-1", Total = 20m };

        // Act
        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 5m });
        await handler.RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken);
        var afterPartialRefund = handler.Revoked.Count;

        order.Refunds.Add(new OrderRefund { RefundId = "r2", Amount = 15m });
        await handler.RefundedAsync(new OrderRefundedContext(order, order.Refunds[1]), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, afterPartialRefund);
        Assert.Equal(OrderRevokeReason.Refunded, Assert.Single(handler.Revoked).Reason);
    }

    private sealed class RecordingGrantHandler : OrderGrantHandlerBase
    {
        public List<Order> Granted { get; } = [];

        public List<(Order Order, OrderRevokeReason Reason)> Revoked { get; } = [];

        protected override Task GrantAsync(Order order, CancellationToken cancellationToken)
        {
            Granted.Add(order);

            return Task.CompletedTask;
        }

        protected override Task RevokeAsync(Order order, OrderRevokeReason reason, CancellationToken cancellationToken)
        {
            Revoked.Add((order, reason));

            return Task.CompletedTask;
        }
    }
}
