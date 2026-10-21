using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Orders.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Tells webhooks when an order is placed, paid, canceled or refunded. The order is sent in the shape the commerce API
/// returns it, so a receiver reads one format.
/// </summary>
internal sealed class OrderWebhookEventHandler : OrderEventHandlerBase
{
    private readonly ICommerceWebhookPublisher _publisher;
    private readonly IServiceProvider _serviceProvider;

    public OrderWebhookEventHandler(
        ICommerceWebhookPublisher publisher,
        IServiceProvider serviceProvider)
    {
        _publisher = publisher;
        _serviceProvider = serviceProvider;
    }

    // Another handler (the stock hold) can still refuse the order after this one has heard of it, so it's told only once
    // the order is saved and still stands.
    public override Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
    {
        if (context.Problems.Count > 0)
        {
            return Task.CompletedTask;
        }

        var orderId = context.Order.ItemId;

        ShellScope.AddDeferredTask(async scope =>
        {
            if (await scope.ServiceProvider.GetRequiredService<IOrderStore>().FindByIdAsync(orderId) is { Status: not OrderStatus.Canceled } order)
            {
                await scope.ServiceProvider.GetRequiredService<ICommerceWebhookPublisher>()
                    .PublishAsync(CommerceWebhookEvents.OrderCreated, new { order = await ToApiAsync(scope.ServiceProvider, order) }, $"evt_order-created_{order.ItemId}");
            }
        });

        return Task.CompletedTask;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => await _publisher.PublishAsync(CommerceWebhookEvents.OrderPaid, new { order = await ToApiAsync(_serviceProvider, context.Order) }, cancellationToken);

    // A cart checked out again replaces its earlier unpaid order; that's housekeeping, not news.
    public override async Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
    {
        if (context.Reason == OrderCancelReason.Replaced)
        {
            return;
        }

        await _publisher.PublishAsync(CommerceWebhookEvents.OrderCanceled, new { order = await ToApiAsync(_serviceProvider, context.Order), reason = OrderApiMapper.ToCamel(context.Reason.ToString()) }, cancellationToken);
    }

    public override async Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => await _publisher.PublishAsync(CommerceWebhookEvents.OrderRefunded, new
        {
            order = await ToApiAsync(_serviceProvider, context.Order),
            refund = new ApiOrderRefund(context.Refund.RefundId, Commerce.Api.ApiMoney.From(context.Refund.Amount, context.Refund.Currency ?? context.Order.Currency), context.Refund.Reason, context.Refund.CreatedUtc),
        }, cancellationToken);

    // Whether money is still owed is worked out as the API does, so a receiver reads the same answer. The operations
    // service is resolved here because it needs IOrderService, which is built with every order event handler, this one
    // included.
    private static async Task<ApiOrder> ToApiAsync(IServiceProvider services, Order order)
        => OrderApiMapper.ToApi(
            order,
            await services.GetRequiredService<OrderOperationsService>().IsOwedAsync(order),
            payUrl: null);
}
