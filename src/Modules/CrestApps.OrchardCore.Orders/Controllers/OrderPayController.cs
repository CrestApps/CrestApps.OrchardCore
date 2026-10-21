using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Pays an order the store sent the buyer: opens its checkout, or the one already open, from the order's signed link
/// or from the buyer's account.
/// </summary>
public sealed class OrderPayController : Controller
{
    private readonly IOrderStore _orders;
    private readonly IOrderLinkService _links;
    private readonly ICheckoutEngine _engine;
    private readonly ICheckoutSessionStore _sessions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    public OrderPayController(
        IOrderStore orders,
        IOrderLinkService links,
        ICheckoutEngine engine,
        ICheckoutSessionStore sessions,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _orders = orders;
        _links = links;
        _engine = engine;
        _sessions = sessions;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    [HttpGet("orders/view/{token}/pay", Name = "GuestOrderPay")]
    public async Task<IActionResult> Guest(string token)
    {
        var orderId = string.IsNullOrEmpty(token) ? null : _links.GetOrderId(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted);

        return order is null ? NotFound() : await PayAsync(order, Url.RouteUrl(OrderConstants.RouteNames.View, new { token }));
    }

    [HttpGet("account/orders/{orderId}/pay", Name = "AccountOrderPay")]
    public async Task<IActionResult> Account(string orderId)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null ||
            !string.Equals(order.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return NotFound();
        }

        return await PayAsync(order, Url.RouteUrl("AccountOrder", new { orderId }));
    }

    private async Task<IActionResult> PayAsync(Order order, string orderUrl)
    {
        // Paid already, canceled, or not an order to pay this way: the order's page says where it stands.
        if (order.Invoice is null || order.Status is not (OrderStatus.Draft or OrderStatus.AwaitingPayment) ||
            !Services.QuoteService.CanPay(order, _clock.UtcNow))
        {
            return Redirect(orderUrl);
        }

        if (order.Status == OrderStatus.AwaitingPayment && !string.IsNullOrEmpty(order.CheckoutSessionId) &&
            await _sessions.GetAsync(order.CheckoutSessionId, CheckoutSessionStatus.Pending) is { } open)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = open.SessionId });
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
        }, HttpContext.RequestAborted);

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId, step = session.CurrentStep });
    }
}
