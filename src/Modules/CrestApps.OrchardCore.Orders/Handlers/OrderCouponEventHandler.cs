using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Gives back the coupon uses of a canceled order, so a code isn't used up by an order that never happened.
/// </summary>
internal sealed class OrderCouponEventHandler : OrderEventHandlerBase
{
    private readonly ICouponRedemptionService _redemptions;

    public OrderCouponEventHandler(ICouponRedemptionService redemptions)
    {
        _redemptions = redemptions;
    }

    public override async Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(context.Order.CheckoutSessionId))
        {
            await _redemptions.ReturnAsync(context.Order.CheckoutSessionId, cancellationToken);
        }
    }
}
