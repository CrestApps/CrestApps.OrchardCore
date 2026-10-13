using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Tells a customer their order number on the checkout's confirmation page, so a guest has something to quote.
/// </summary>
public sealed class OrderConfirmationDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly IOrderStore _orderStore;

    public OrderConfirmationDisplayDriver(IOrderStore orderStore)
    {
        _orderStore = orderStore;
    }

    public override async Task<IDisplayResult> DisplayAsync(CheckoutFlow model, BuildDisplayContext context)
    {
        if (context.DisplayType != "Confirmation" ||
            model?.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(session.ReferenceId))
        {
            return null;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null)
        {
            return null;
        }

        return Initialize<OrderConfirmationViewModel>("OrderConfirmation", confirmation =>
        {
            confirmation.OrderId = order.ItemId;
            confirmation.OrderNumber = order.OrderNumber;
            confirmation.IsSignedIn = order.UserId is not null;
        }).Location("Confirmation", "Content:0");
    }
}
