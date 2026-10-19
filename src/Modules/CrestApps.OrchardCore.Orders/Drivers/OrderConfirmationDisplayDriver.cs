using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
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
    private readonly OrderAccountClaims _accountClaims;
    private readonly IContentManager _contentManager;

    public OrderConfirmationDisplayDriver(
        IOrderStore orderStore,
        IOrderLinkService orderLinks,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        OrderAccountClaims accountClaims,
        IContentManager contentManager)
    {
        _contentManager = contentManager;
        _orderStore = orderStore;
        _orderLinks = orderLinks;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _accountClaims = accountClaims;
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

        return Combine(Initialize<OrderConfirmationViewModel>("OrderConfirmation", async confirmation =>
        {
            confirmation.OrderId = order.ItemId;
            confirmation.OrderNumber = order.OrderNumber;
            confirmation.IsSignedIn = order.UserId is not null;

            // A guest keeps a link to the order, and can make an account right here to find it in their account. The
            // registration page only links when the site lets visitors register, which is the same rule for this form.
            if (order.UserId is null && _httpContextAccessor.HttpContext is { } httpContext)
            {
                confirmation.GuestOrderUrl = await _orderLinks.CreateUrlAsync(order);
                confirmation.RegisterUrl = _linkGenerator.GetPathByAction(httpContext, "Register", "Registration", new { area = "OrchardCore.Users" });

                if (!string.IsNullOrEmpty(confirmation.RegisterUrl) && !string.IsNullOrWhiteSpace(order.CustomerEmail) && httpContext.User.Identity?.IsAuthenticated != true)
                {
                    confirmation.AccountToken = _accountClaims.Create(order.ItemId);
                    confirmation.Email = order.CustomerEmail;
                    confirmation.ReturnUrl = httpContext.Request.PathBase + httpContext.Request.Path;
                }
            }
        }).Location("Confirmation", "Content:0"),
        Initialize<OrderConfirmationOffersViewModel>("OrderConfirmationOffers", async offers =>
        {
            // What goes with what was just bought, offered while the buyer is still here.
            offers.Products = [.. await _contentManager.GetAsync(order.Lines.Select(line => line.ContentItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal), VersionOptions.Published)];
        }).Location("Confirmation", "Content:20"));
    }
}
