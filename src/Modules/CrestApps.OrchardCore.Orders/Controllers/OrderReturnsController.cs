using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The buyer's side of returns: the form to ask to send items back, from their account or from an order's signed
/// link, and withdrawing a request the store hasn't answered. The form is the <c>OrderReturnRequest</c> shape.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
public sealed class OrderReturnsController : Controller
{
    private readonly IOrderStore _orders;
    private readonly IOrderReturnStore _returns;
    private readonly IOrderLinkService _links;
    private readonly OrderReturnService _returnService;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;

    public OrderReturnsController(
        IOrderStore orders,
        IOrderReturnStore returns,
        IOrderLinkService links,
        OrderReturnService returnService,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory)
    {
        _orders = orders;
        _returns = returns;
        _links = links;
        _returnService = returnService;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/orders/{orderId}/return", Name = "AccountOrderReturn")]
    public async Task<IActionResult> Start(string orderId)
    {
        var order = await FindOwnAsync(orderId);

        return order is null ? NotFound() : await FormAsync(order, posted: null, problems: [], Url.RouteUrl("AccountOrder", new { orderId }), "Request");
    }

    [HttpPost("account/orders/{orderId}/return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPost(string orderId, OrderReturnRequestViewModel model)
    {
        var order = await FindOwnAsync(orderId);

        if (order is null)
        {
            return NotFound();
        }

        var backUrl = Url.RouteUrl("AccountOrder", new { orderId });
        var (_, problems) = await _returnService.RequestAsync(order, ToRequest(model), HttpContext.RequestAborted);

        return problems.Count > 0 ? await FormAsync(order, model, problems, backUrl, "Request") : Redirect(backUrl);
    }

    [HttpGet("orders/view/{token}/return", Name = "GuestOrderReturn")]
    public async Task<IActionResult> Guest(string token)
    {
        var order = await FindByTokenAsync(token);

        return order is null ? NotFound() : await FormAsync(order, posted: null, problems: [], Url.RouteUrl(OrderConstants.RouteNames.View, new { token }), "Guest");
    }

    [HttpPost("orders/view/{token}/return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuestPost(string token, OrderReturnRequestViewModel model)
    {
        var order = await FindByTokenAsync(token);

        if (order is null)
        {
            return NotFound();
        }

        var backUrl = Url.RouteUrl(OrderConstants.RouteNames.View, new { token });
        var (_, problems) = await _returnService.RequestAsync(order, ToRequest(model), HttpContext.RequestAborted);

        return problems.Count > 0 ? await FormAsync(order, model, problems, backUrl, "Guest") : Redirect(backUrl);
    }

    [HttpPost("account/orders/{orderId}/returns/{returnId}/withdraw", Name = "AccountOrderReturnWithdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(string orderId, string returnId)
    {
        var order = await FindOwnAsync(orderId);

        if (order is null)
        {
            return NotFound();
        }

        await WithdrawAsync(order, returnId);

        return RedirectToRoute("AccountOrder", new { orderId });
    }

    [HttpPost("orders/view/{token}/returns/{returnId}/withdraw", Name = "GuestOrderReturnWithdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuestWithdraw(string token, string returnId)
    {
        var order = await FindByTokenAsync(token);

        if (order is null)
        {
            return NotFound();
        }

        await WithdrawAsync(order, returnId);

        return RedirectToRoute(OrderConstants.RouteNames.View, new { token });
    }

    private async Task WithdrawAsync(Order order, string returnId)
    {
        if (!string.IsNullOrEmpty(returnId) &&
            await _returns.FindByIdAsync(returnId, HttpContext.RequestAborted) is { } orderReturn &&
            orderReturn.OrderId == order.ItemId)
        {
            await _returnService.WithdrawAsync(orderReturn, HttpContext.RequestAborted);
        }
    }

    private async Task<IActionResult> FormAsync(Order order, OrderReturnRequestViewModel posted, IReadOnlyList<string> problems, string backUrl, string viewName)
    {
        var returnable = await _returnService.GetReturnableAsync(order, HttpContext.RequestAborted);

        // Nothing left to return, or the window closed: back to the order, which says so.
        if (returnable.Count == 0 && problems.Count == 0)
        {
            return Redirect(backUrl);
        }

        var settings = await _siteService.GetSettingsAsync<ReturnSettings>();

        return View(viewName, await _shapeFactory.CreateAsync<OrderReturnRequestViewModel>("OrderReturnRequest", model =>
        {
            model.Order = order;
            model.Reasons = settings.GetReasons();
            model.AllowExchanges = settings.AllowExchanges;
            model.WindowDays = settings.WindowDays;
            model.Problems = problems;
            model.BackUrl = backUrl;
            model.Resolution = posted?.Resolution ?? OrderReturnResolution.Refund;
            model.ExchangeFor = posted?.ExchangeFor;
            model.Comment = posted?.Comment;
            model.Lines = order.Lines
                .Where(line => returnable.ContainsKey(line.LineId))
                .Select(line =>
                {
                    var previous = posted?.Lines?.FirstOrDefault(entry => entry?.LineId == line.LineId);

                    return new OrderReturnRequestLineViewModel
                    {
                        LineId = line.LineId,
                        Title = OrderLineText.Describe(line),
                        Sku = line.Sku,
                        Returnable = returnable[line.LineId],
                        Quantity = previous?.Quantity ?? 0,
                        Reason = previous?.Reason,
                    };
                })
                .ToList();
        }));
    }

    private static OrderReturnRequest ToRequest(OrderReturnRequestViewModel model)
        => new()
        {
            Lines = (model?.Lines ?? [])
                .Where(line => !string.IsNullOrEmpty(line?.LineId))
                .Select(line => (line.LineId, line.Quantity, line.Reason))
                .ToArray(),
            Resolution = model?.Resolution ?? OrderReturnResolution.Refund,
            ExchangeFor = model?.ExchangeFor,
            Comment = model?.Comment,
        };

    // Only the customer's own placed orders; anything else reads as not found.
    private async Task<Order> FindOwnAsync(string orderId)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted);

        return order is null ||
            order.Status != OrderStatus.Paid ||
            !string.Equals(order.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders)
            ? null
            : order;
    }

    private async Task<Order> FindByTokenAsync(string token)
    {
        var orderId = string.IsNullOrEmpty(token) ? null : _links.GetOrderId(token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted);

        return order is { Status: OrderStatus.Paid } ? order : null;
    }
}
