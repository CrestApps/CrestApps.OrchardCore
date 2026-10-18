using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The signed-in customer's orders on the storefront account: the list, and each order with what other modules add
/// to it, such as its downloads and shipments.
/// </summary>
[Authorize]
public sealed class AccountOrdersController : Controller
{
    private const int PageSize = 10;

    private readonly IOrderStore _orderStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;

    public AccountOrdersController(IOrderStore orderStore, IAuthorizationService authorizationService, IShapeFactory shapeFactory)
    {
        _orderStore = orderStore;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/orders", Name = "AccountOrders")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, PageSize);
        var (count, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Statuses = [OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Canceled],
        }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new AccountOrdersViewModel
        {
            Orders = orders,
            Pager = count > PageSize ? await _shapeFactory.PagerAsync(pager, count) : null,
        });
    }

    [HttpGet("account/orders/{orderId}", Name = "AccountOrder")]
    public async Task<IActionResult> Detail(
        string orderId,
        [FromServices] IDisplayManager<Order> displayManager,
        [FromServices] IUpdateModelAccessor updateModelAccessor)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        // Only the customer's own placed orders open here; anything else reads as not found.
        if (order is null ||
            order.Status == OrderStatus.Draft ||
            !string.Equals(order.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return NotFound();
        }

        return View(new OrderViewViewModel
        {
            Order = order,
            Sections = await displayManager.BuildDisplayAsync(order, updateModelAccessor.ModelUpdater, OrderConstants.DisplayTypes.Detail),
            PayUrl = order.Status == OrderStatus.Canceled ? null : await OrderPayLinks.GetPayUrlAsync(HttpContext.RequestServices, order),
            InAccount = true,
        });
    }
}
