using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// What the buyer entered in the store's checkout fields, on the order's pages for the store and the buyer.
/// </summary>
internal sealed class OrderCheckoutDetailsDisplayDriver : DisplayDriver<Order>
{
    private readonly CheckoutDetailsForm _form;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;

    public OrderCheckoutDetailsDisplayDriver(
        CheckoutDetailsForm form,
        IContentItemDisplayManager contentItemDisplayManager)
    {
        _form = form;
        _contentItemDisplayManager = contentItemDisplayManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrderConstants.DisplayTypes.DetailAdmin or OrderConstants.DisplayTypes.Detail) ||
            order.CheckoutDetails is null ||
            order.CheckoutDetails.Count == 0)
        {
            return null;
        }

        var item = await _form.NewAsync(order.CheckoutDetails);
        var display = await _contentItemDisplayManager.BuildDisplayAsync(item, context.Updater, "Detail");

        return Initialize<CheckoutDetailsViewModel>("OrderCheckoutDetails", model => model.Content = display)
            .Location("Content:3");
    }
}
