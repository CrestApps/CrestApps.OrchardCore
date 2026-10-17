using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.RateLimits;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Turns the visitor's cart into an order and sends them to pay for it.
/// </summary>
public sealed class CartCheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly ICheckoutEngine _engine;
    private readonly INotifier _notifier;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;

    internal readonly IHtmlLocalizer H;

    public CartCheckoutController(
        ICartService cartService,
        IOrderService orderService,
        ICheckoutEngine engine,
        INotifier notifier,
        UserManager<IUser> userManager,
        ISession session,
        IHtmlLocalizer<CartCheckoutController> htmlLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _engine = engine;
        _notifier = notifier;
        _userManager = userManager;
        _session = session;
        H = htmlLocalizer;
    }

    [HttpPost("cart/checkout", Name = CartConstants.RouteNames.Checkout)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> Checkout()
    {
        var cancellationToken = HttpContext.RequestAborted;

        // Priced again here, not taken from the page: the cart may have changed in another window, and a price
        // may have changed since the page was shown.
        var pricing = await _cartService.PriceAsync(await _cartService.GetCurrentAsync(cancellationToken), cancellationToken);

        if (!pricing.CanCheckout)
        {
            await _notifier.ErrorAsync(pricing.Lines.Count == 0
                ? H["Your cart is empty."]
                : H["Some items in your cart can't be bought as they are. Change or remove them to check out."]);

            return RedirectToRoute(CartConstants.RouteNames.Cart);
        }

        var created = await _orderService.CreateFromCartAsync(pricing, cancellationToken);

        // Another buyer may have just taken the last units.
        if (!created.Succeeded)
        {
            foreach (var problem in created.Problems)
            {
                // The problem names products, so it goes in as an argument, which is encoded.
                await _notifier.ErrorAsync(H["{0}", problem]);
            }

            return RedirectToRoute(CartConstants.RouteNames.Cart);
        }

        var order = created.Order;

        // A guest's details come from the checkout; a signed-in customer's are already known.
        if (User.Identity?.IsAuthenticated == true && await _userManager.GetUserAsync(User) is User user)
        {
            order.CustomerName = user.UserName;
            order.CustomerEmail = user.Email;

            // With Customers enabled, the order belongs to the user's customer, made on their first order.
            if (HttpContext.RequestServices.GetService<ICustomerService>() is { } customers &&
                await customers.GetOrCreateForUserAsync(user.UserId, cancellationToken) is { } customer)
            {
                order.CustomerId = customer.ItemId;
                order.CustomerName = customer.GetName() ?? user.UserName;

                await customers.TouchAsync(customer, cancellationToken);
            }
        }

        // Committed here, with the stock the order holds, so a buyer who just took the last units elsewhere makes this one
        // fail cleanly instead of selling them twice.
        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();
            await _notifier.ErrorAsync(H["Someone else is buying the same items right now. Try checking out again."]);

            return RedirectToRoute(CartConstants.RouteNames.Cart);
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
        }, cancellationToken);

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }
}
