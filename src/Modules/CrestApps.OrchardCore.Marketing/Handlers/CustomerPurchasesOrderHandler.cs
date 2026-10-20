using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Marketing.Handlers;

/// <summary>
/// Counts an order in its customer's purchases when it's paid, and takes refunds off.
/// </summary>
internal sealed class CustomerPurchasesOrderHandler : OrderEventHandlerBase
{
    private readonly CustomerPurchasesService _purchases;

    public CustomerPurchasesOrderHandler(CustomerPurchasesService purchases)
    {
        _purchases = purchases;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _purchases.RecordAsync(context.Order, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => _purchases.RecordAsync(context.Order, cancellationToken);
}
