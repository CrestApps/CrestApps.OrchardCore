using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Marketplace.Handlers;

/// <summary>
/// Records what vendors earn when an order is paid, and takes back their share when it's refunded.
/// </summary>
internal sealed class MarketplaceOrderEventHandler : OrderEventHandlerBase
{
    private readonly MarketplaceService _marketplace;

    public MarketplaceOrderEventHandler(MarketplaceService marketplace)
    {
        _marketplace = marketplace;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _marketplace.RecordSaleAsync(context.Order, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => _marketplace.RecordRefundAsync(context.Order, context.Refund, cancellationToken);
}
