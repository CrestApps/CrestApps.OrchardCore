using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Orders.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Tells webhooks when an order is placed, paid, canceled or refunded. The order is sent in the shape the commerce API
/// returns it, so a receiver reads one format.
/// </summary>
internal sealed class OrderWebhookEventHandler : OrderEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public OrderWebhookEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
        => context.Problems.Count > 0
            ? Task.CompletedTask
            : PublishAsync(CommerceWebhookEvents.OrderCreated, new { order = ToApi(context.Order) }, cancellationToken);

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => PublishAsync(CommerceWebhookEvents.OrderPaid, new { order = ToApi(context.Order) }, cancellationToken);

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => PublishAsync(CommerceWebhookEvents.OrderCanceled, new { order = ToApi(context.Order), reason = OrderApiMapper.ToCamel(context.Reason.ToString()) }, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => PublishAsync(CommerceWebhookEvents.OrderRefunded, new
        {
            order = ToApi(context.Order),
            refund = new ApiOrderRefund(context.Refund.RefundId, Commerce.Api.ApiMoney.From(context.Refund.Amount, context.Refund.Currency ?? context.Order.Currency), context.Refund.Reason, context.Refund.CreatedUtc),
        }, cancellationToken);

    private static ApiOrder ToApi(Order order)
        => OrderApiMapper.ToApi(order, isOwed: order.Status == OrderStatus.AwaitingPayment, payUrl: null);

    // Resolved when an event is raised: with the Commerce Webhooks feature off there's no publisher.
    private Task PublishAsync(string eventName, object data, CancellationToken cancellationToken)
        => _serviceProvider.GetService<ICommerceWebhookPublisher>() is { } publisher
            ? publisher.PublishAsync(eventName, data, cancellationToken)
            : Task.CompletedTask;
}
