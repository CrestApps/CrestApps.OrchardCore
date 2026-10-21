using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Orders.Workflows;

/// <summary>
/// Starts the order workflow events as orders are paid, canceled and refunded.
/// </summary>
internal sealed class OrderWorkflowHandler : OrderEventHandlerBase
{
    private readonly IWorkflowManager _workflowManager;

    public OrderWorkflowHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(OrderPaidEvent), context.Order, input => { });

    // A draft that never reached its checkout is canceled when its cart is checked out again; that isn't news.
    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => context.Reason == OrderCancelReason.Replaced
            ? Task.CompletedTask
            : TriggerAsync(nameof(OrderCanceledEvent), context.Order, input =>
            {
                input["Reason"] = context.Reason.ToString();
                input["Note"] = context.Order.CancelNote;
            });

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(OrderRefundedEvent), context.Order, input =>
        {
            input["Amount"] = context.Refund.Amount;
            input["Reason"] = context.Refund.Reason;
            input["RefundedAmount"] = context.Order.RefundedAmount;
            input["FullyRefunded"] = context.Order.RefundStatus == OrderRefundStatus.Refunded;
        });

    private async Task TriggerAsync(string eventName, Order order, Action<Dictionary<string, object>> add)
    {
        var input = new Dictionary<string, object>
        {
            ["Order"] = order,
            ["OrderId"] = order.ItemId,
            ["OrderNumber"] = order.OrderNumber,
            ["Total"] = order.Total,
            ["Currency"] = order.Currency,
            ["CustomerEmail"] = order.CustomerEmail,
        };

        add(input);

        await _workflowManager.TriggerEventAsync(eventName, input, correlationId: order.ItemId);
    }
}
