using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Where a recovery email's link lands: back in the order's checkout when it is still open, otherwise a cart with the
/// same items at today's prices.
/// </summary>
public sealed class OrderRecoveryController : Controller
{
    private readonly IOrderStore _orderStore;
    private readonly CheckoutRecoveryLinks _links;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICartService _cartService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public OrderRecoveryController(
        IOrderStore orderStore,
        CheckoutRecoveryLinks links,
        ICheckoutSessionStore sessionStore,
        ICartService cartService,
        INotifier notifier,
        IHtmlLocalizer<OrderRecoveryController> htmlLocalizer)
    {
        _orderStore = orderStore;
        _links = links;
        _sessionStore = sessionStore;
        _cartService = cartService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("orders/resume/{token}", Name = "OrderResume")]
    public async Task<IActionResult> Resume(string token)
    {
        var orderId = _links.Read(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null)
        {
            await _notifier.WarningAsync(H["That link has expired. Your cart is below."]);

            return RedirectToRoute(CartConstants.RouteNames.Cart);
        }

        if (order.Status == OrderStatus.Paid)
        {
            await _notifier.InformationAsync(H["Order {0} is already paid. Thank you!", order.OrderNumber]);

            return LocalRedirect(Url.Content("~/"));
        }

        // The checkout is still open: carry on where they left it.
        if (order.Status == OrderStatus.AwaitingPayment &&
            !string.IsNullOrEmpty(order.CheckoutSessionId) &&
            await _sessionStore.GetAsync(order.CheckoutSessionId, CheckoutSessionStatus.Pending) is { } session)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });
        }

        var added = 0;

        foreach (var line in order.Lines ?? [])
        {
            // The add-ons come back too, so an engraving isn't lost on the way back to the cart.
            var addOns = line.AddOns is { Count: > 0 }
                ? line.AddOns.Where(addOn => !string.IsNullOrEmpty(addOn.AddOnId)).ToDictionary(addOn => addOn.AddOnId, addOn => addOn.Value ?? "true", StringComparer.Ordinal)
                : null;

            var result = await _cartService.AddAsync(line.ContentItemId, line.PriceId, line.VariantId, line.Quantity, addOns, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                added++;
            }
        }

        if (added == 0)
        {
            await _notifier.WarningAsync(H["The items you chose aren't available any more."]);
        }
        else if (added < (order.Lines?.Count ?? 0))
        {
            await _notifier.InformationAsync(H["Your items are back in your cart, except some that aren't available any more."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Your items are back in your cart."]);
        }

        return RedirectToRoute(CartConstants.RouteNames.Cart);
    }
}
