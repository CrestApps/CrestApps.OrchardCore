using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The cart page's Checkout button: turns the cart into an order and opens its checkout.
/// </summary>
public sealed class CartCheckoutController : Controller
{
    private readonly CartCheckoutService _cartCheckout;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public CartCheckoutController(
        CartCheckoutService cartCheckout,
        INotifier notifier,
        IHtmlLocalizer<CartCheckoutController> htmlLocalizer)
    {
        _cartCheckout = cartCheckout;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost("cart/checkout", Name = CartConstants.RouteNames.Checkout)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> Checkout()
    {
        var result = await _cartCheckout.StartAsync(User, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var problem in result.Problems)
            {
                // A problem can name products, so it goes in as an argument, which is encoded.
                await _notifier.ErrorAsync(H["{0}", problem]);
            }

            return RedirectToRoute(CartConstants.RouteNames.Cart);
        }

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = result.Session.SessionId,
            step = result.Session.CurrentStep,
        });
    }
}
