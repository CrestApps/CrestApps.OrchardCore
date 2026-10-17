using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Tells a customer their order number on the checkout's confirmation page, so a guest has something to quote.
/// </summary>
public sealed class OrderConfirmationDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly IOrderStore _orderStore;
    private readonly IOrderLinkService _orderLinks;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public OrderConfirmationDisplayDriver(
        IOrderStore orderStore,
        IOrderLinkService orderLinks,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _orderStore = orderStore;
        _orderLinks = orderLinks;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
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

        return Initialize<OrderConfirmationViewModel>("OrderConfirmation", async confirmation =>
        {
            confirmation.OrderId = order.ItemId;
            confirmation.OrderNumber = order.OrderNumber;
            confirmation.IsSignedIn = order.UserId is not null;

            // A guest keeps a link to the order, and can create an account to find it under My Orders.
            if (order.UserId is null && _httpContextAccessor.HttpContext is { } httpContext)
            {
                confirmation.GuestOrderUrl = await _orderLinks.CreateUrlAsync(order);
                confirmation.RegisterUrl = _linkGenerator.GetPathByAction(httpContext, "Register", "Registration", new { area = "OrchardCore.Users" });
            }
        }).Location("Confirmation", "Content:0");
    }
}
