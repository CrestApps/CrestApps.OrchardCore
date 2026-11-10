using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// Tells agents' platforms when the orders their agents placed are paid, canceled, refunded or returned.
/// </summary>
public sealed class AgentOrderEventHandler : OrderEventHandlerBase
{
    private readonly AgentOrderUpdates _updates;

    public AgentOrderEventHandler(AgentOrderUpdates updates)
    {
        _updates = updates;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _updates.PublishAsync(context.Order, created: true, cancellationToken);

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => _updates.PublishAsync(context.Order, created: false, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => _updates.PublishAsync(context.Order, created: false, cancellationToken);

    public override Task ReturnedAsync(OrderReturnedContext context, CancellationToken cancellationToken = default)
        => _updates.PublishAsync(context.Order, created: false, cancellationToken);
}
