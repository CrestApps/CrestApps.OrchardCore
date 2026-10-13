using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Carts.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Admin;
using OrchardCore.Admin.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Carts.Drivers;

/// <summary>
/// Puts a link to the cart, with its item count, in the site's navigation bar, beside the user menu. A theme
/// restyles it by overriding the <c>CartLink</c> shape, or hides it with an empty template.
/// </summary>
public sealed class CartNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly ICartService _cartService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public CartNavbarDisplayDriver(
        ICartService cartService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _cartService = cartService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // The admin bar is not the shop's.
        if (httpContext is null || AdminAttribute.IsApplied(httpContext))
        {
            return null;
        }

        return Initialize<CartLinkViewModel>("CartLink", async link =>
        {
            var cart = await _cartService.GetCurrentAsync(httpContext.RequestAborted);

            link.ItemCount = cart?.ItemCount ?? 0;
            link.Url = _linkGenerator.GetPathByRouteValues(httpContext, CartConstants.RouteNames.Cart, values: null);
        }).Location("Detail", "Content:6");
    }
}
