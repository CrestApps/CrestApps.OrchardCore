using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Marketplace.Handlers;

/// <summary>
/// Records what vendors earn when an order is paid, and takes back their share when it's refunded.
/// </summary>
internal sealed class MarketplaceOrderEventHandler : OrderEventHandlerBase
{
    // Resolved when an order changes, so the order service this handler is part of isn't needed to build it.
    private readonly IServiceProvider _serviceProvider;

    public MarketplaceOrderEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private MarketplaceService Marketplace
        => _serviceProvider.GetRequiredService<MarketplaceService>();

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => Marketplace.RecordSaleAsync(context.Order, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => Marketplace.RecordRefundAsync(context.Order, context.Refund, cancellationToken);
}
