using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Routing;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The store's orders, and a customer's own orders.
/// </summary>
public sealed class OrdersAdminController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string OptionsStatus = "Options.Status";

    private readonly IOrderStore _orderStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;

    public OrdersAdminController(
        IOrderStore orderStore,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<OrdersAdminController> stringLocalizer)
    {
        _orderStore = orderStore;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
    }

    [Admin("orders", "OrdersIndex")]
    public async Task<IActionResult> Index(OrdersIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        return View(await BuildListAsync(options, pagerParameters, userId: null));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("orders", "OrdersIndex")]
    public async Task<IActionResult> IndexFilterPost(OrdersIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), FilterRouteValues(options, pagerParameters));
    }

    [Admin("my-orders", "MyOrders")]
    public async Task<IActionResult> Mine(PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
        {
            return Forbid();
        }

        var model = await BuildListAsync(new OrdersIndexOptions(), pagerParameters, User.FindFirstValue(ClaimTypes.NameIdentifier));
        model.IsOwnOrders = true;

        return View(nameof(Index), model);
    }

    [Admin("orders/{orderId}", "OrdersDetail")]
    public async Task<IActionResult> Detail(string orderId)
    {
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId);

        if (order is null)
        {
            return NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders);

        if (!canManage)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // A customer sees their own orders, and only ones they actually placed.
            if (order.UserId is null ||
                order.UserId != userId ||
                order.Status is OrderStatus.Draft or OrderStatus.Canceled ||
                !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ViewOwnOrders))
            {
                return Forbid();
            }
        }

        return View(new OrderDetailViewModel { Order = order, CanManage = canManage });
    }

    private async Task<OrdersIndexViewModel> BuildListAsync(OrdersIndexOptions options, PagerParameters pagerParameters, string userId)
    {
        options ??= new OrdersIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var query = new OrderQuery
        {
            UserId = userId,
            Search = userId is null ? options.Search : null,
            Statuses = userId is not null
                ? [OrderStatus.Paid, OrderStatus.AwaitingPayment]
                : options.Status switch
                {
                    OrderStatusFilter.Paid => [OrderStatus.Paid],
                    OrderStatusFilter.AwaitingPayment => [OrderStatus.AwaitingPayment],
                    OrderStatusFilter.Canceled => [OrderStatus.Canceled],
                    OrderStatusFilter.All => [],
                    _ => [OrderStatus.Paid, OrderStatus.AwaitingPayment],
                },
        };

        var (count, orders) = await _orderStore.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Statuses =
        [
            new SelectListItem(S["Placed"], nameof(OrderStatusFilter.Placed), options.Status == OrderStatusFilter.Placed),
            new SelectListItem(S["Paid"], nameof(OrderStatusFilter.Paid), options.Status == OrderStatusFilter.Paid),
            new SelectListItem(S["Awaiting payment"], nameof(OrderStatusFilter.AwaitingPayment), options.Status == OrderStatusFilter.AwaitingPayment),
            new SelectListItem(S["Canceled"], nameof(OrderStatusFilter.Canceled), options.Status == OrderStatusFilter.Canceled),
            new SelectListItem(S["Any status"], nameof(OrderStatusFilter.All), options.Status == OrderStatusFilter.All),
        ];

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(OptionsSearch, options.Search);
        }

        if (options.Status != OrderStatusFilter.Placed)
        {
            routeData.Values.TryAdd(OptionsStatus, options.Status);
        }

        var viewModel = new OrdersIndexViewModel
        {
            Options = options,
            Orders = orders,
            TotalCount = count,
            Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
        };

        // The admin-list tag builds a row per order, in the layout (List or Grid) the site or the user picked.
        viewModel.List = new AdminListContext<Order>
        {
            Source = orders,
            Pager = viewModel.Pager,
        };

        // The filters render at the end of the list's toolbar; a customer's own orders are not filtered.
        if (userId is null)
        {
            viewModel.List.ToolbarActions = await _shapeFactory.CreateAsync("OrdersAdminListFilters", Arguments.From(new { Options = options }));
        }

        return viewModel;
    }

    private static RouteValueDictionary FilterRouteValues(OrdersIndexOptions options, PagerParameters pagerParameters)
    {
        var values = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options?.Search))
        {
            values.Add(OptionsSearch, options.Search);
        }

        if (options is not null && options.Status != OrderStatusFilter.Placed)
        {
            values.Add(OptionsStatus, options.Status);
        }

        if (pagerParameters?.PageSize is int pageSize)
        {
            values.Add("pageSize", pageSize);
        }

        return values;
    }
}
