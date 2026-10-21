using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// An order event handler that gives the buyer what an order bought when the order is paid, such as files, a membership
/// or a gift card, and takes it back when the order is canceled or refunded in full. After a partial refund the buyer
/// keeps what they bought.
/// </summary>
public abstract class OrderGrantHandlerBase : OrderEventHandlerBase
{
    /// <inheritdoc />
    public sealed override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return GrantAsync(context.Order, cancellationToken);
    }

    /// <inheritdoc />
    public sealed override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return RevokeAsync(context.Order, OrderRevokeReason.Canceled, cancellationToken);
    }

    /// <inheritdoc />
    public sealed override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Order.RefundStatus == OrderRefundStatus.Refunded
            ? RevokeAsync(context.Order, OrderRevokeReason.Refunded, cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Gives the buyer what the paid order bought. It may be called again for the same order, so it gives only what is
    /// missing.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task GrantAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Takes back what the order gave its buyer.
    /// </summary>
    /// <param name="order">The order canceled or refunded in full.</param>
    /// <param name="reason">Why it is taken back.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task RevokeAsync(Order order, OrderRevokeReason reason, CancellationToken cancellationToken);
}
