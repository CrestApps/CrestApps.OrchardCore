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
/// to it, such as its downloads and shipments. Each page's content is a shape (<c>AccountOrders</c>, <c>AccountOrder</c>)
/// that a theme can override.
/// </summary>
[Authorize]
public sealed class AccountOrdersController : Controller
{
    private const int PageSize = 10;

    private readonly IOrderStore _orderStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly OrderPayLinks _payLinks;

    public AccountOrdersController(
        IOrderStore orderStore,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        OrderPayLinks payLinks)
    {
        _orderStore = orderStore;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _payLinks = payLinks;
    }

    [HttpGet("account/orders", Name = "AccountOrders")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] OrderOperationsService operations)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, PageSize);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var (count, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            UserId = userId,
            Statuses = [OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Canceled],
        }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        // Quotes asked for and orders sent to pay sit apart, above: they aren't orders placed yet.
        var (_, drafts) = await _orderStore.PageAsync(new OrderQuery
        {
            UserId = userId,
            Statuses = [OrderStatus.Draft],
        }, 0, 50, HttpContext.RequestAborted);

        // An order paid offline reads as owed until the money arrives.
        var owed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            if (await operations.IsOwedAsync(order, HttpContext.RequestAborted))
            {
                owed.Add(order.ItemId);
            }
        }

        var pagerShape = count > PageSize ? await _shapeFactory.PagerAsync(pager, count) : null;

        return View(await _shapeFactory.CreateAsync<AccountOrdersViewModel>("AccountOrders", model =>
        {
            model.Orders = orders;
            model.Open = [.. drafts.Where(draft => draft.Quote is not null || draft.Approval is not null || draft.Invoice?.SentUtc is not null)];
            model.OwedOrderIds = owed;
            model.Pager = pagerShape;
        }));
    }

    [HttpGet("account/orders/{orderId}", Name = "AccountOrder")]
    public async Task<IActionResult> Detail(
        string orderId,
        [FromServices] IDisplayManager<Order> displayManager,
        [FromServices] IUpdateModelAccessor updateModelAccessor)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        // Only the customer's own placed orders open here, and orders the store sent them to pay; anything else reads
        // as not found.
        if (order is null ||
            (order.Status == OrderStatus.Draft && order.Invoice?.SentUtc is null && order.Quote is null && order.Approval is null) ||
            !string.Equals(order.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return NotFound();
        }

        var sections = await displayManager.BuildDisplayAsync(order, updateModelAccessor.ModelUpdater, OrderConstants.DisplayTypes.Detail);
        var payUrl = await _payLinks.GetPayUrlAsync(order, Url.RouteUrl("AccountOrderPay", new { orderId = order.ItemId }));

        return View(await _shapeFactory.CreateAsync<OrderViewViewModel>("AccountOrder", model =>
        {
            model.Order = order;
            model.Sections = sections;
            model.PayUrl = payUrl;
            model.InAccount = true;
            model.DeclineQuoteUrl = QuoteService.CanDecline(order) ? Url.RouteUrl("AccountQuoteDecline", new { orderId = order.ItemId }) : null;
            model.InvoiceUrl = Url.RouteUrl("AccountOrderInvoice", new { orderId = order.ItemId });
        }));
    }

    /// <summary>
    /// The buyer's invoice for their order, to print or save as a PDF.
    /// </summary>
    [HttpGet("account/orders/{orderId}/invoice", Name = "AccountOrderInvoice")]
    public async Task<IActionResult> Invoice(string orderId, [FromServices] OrderDocumentBuilder documents)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null ||
            order.Status is OrderStatus.Draft or OrderStatus.Canceled ||
            !string.Equals(order.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return NotFound();
        }

        var document = await documents.BuildInvoiceAsync(order, HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<OrderDocumentViewModel>("OrderInvoice", model => OrdersAdminController.Copy(document, model)));
    }
}
