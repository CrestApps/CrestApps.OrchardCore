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
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Routing;
using OrchardCore.Navigation;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// The store's orders, and a customer's own orders.
/// </summary>
public sealed class OrdersAdminController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string OptionsStatus = "Options.Status";
    private const string OptionsChannel = "Options.Channel";

    private readonly IOrderStore _orderStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public OrdersAdminController(
        IOrderStore orderStore,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<OrdersAdminController> stringLocalizer,
        IHtmlLocalizer<OrdersAdminController> htmlLocalizer)
    {
        _orderStore = orderStore;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
        H = htmlLocalizer;
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

    /// <summary>
    /// The orders the list shows, with its search and status, as a CSV file: a row per line.
    /// </summary>
    [Admin("orders/export", "OrdersExport")]
    public async Task<IActionResult> Export(OrdersIndexOptions options, [FromServices] global::OrchardCore.Modules.ILocalClock localClock, [FromServices] OrderOperationsService operations)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        const int batch = 500;
        var query = BuildQuery(options ?? new OrdersIndexOptions(), userId: null);
        var orders = new List<Order>();

        for (var skip = 0; ; skip += batch)
        {
            var (count, page) = await _orderStore.PageAsync(query, skip, batch, HttpContext.RequestAborted);

            orders.AddRange(page);

            if (page.Count < batch || orders.Count >= count)
            {
                break;
            }
        }

        // Times in the site's time zone, as the list shows them.
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await localClock.GetLocalTimeZoneAsync()).TimeZoneId);
        var owed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            if (await operations.IsOwedAsync(order, HttpContext.RequestAborted))
            {
                owed.Add(order.ItemId);
            }
        }

        var csv = OrderCsv.Write(orders, utc => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone), owed);

        // A byte order mark, so a spreadsheet reads the names and currencies as UTF-8.
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();

        return File(bytes, "text/csv", $"orders-{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone):yyyy-MM-dd}.csv");
    }

    /// <summary>
    /// The order's invoice, to print or save as a PDF.
    /// </summary>
    [Admin("orders/{orderId}/invoice", "OrdersInvoice")]
    public async Task<IActionResult> Invoice(string orderId, [FromServices] OrderDocumentBuilder documents)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null || order.Status == OrderStatus.Draft)
        {
            return NotFound();
        }

        var document = await documents.BuildInvoiceAsync(order, HttpContext.RequestAborted);

        return View("Document", new OrderDocumentPageViewModel
        {
            Order = order,
            IsAdmin = true,
            PrintLabel = S["Print invoice"],
            Document = await _shapeFactory.CreateAsync<OrderDocumentViewModel>("OrderInvoice", model => Copy(document, model)),
        });
    }

    /// <summary>
    /// The order's packing slip: its goods, or one shipment's lines ("lineId:quantity" pairs) with its number.
    /// </summary>
    [Admin("orders/{orderId}/packing-slip", "OrdersPackingSlip")]
    public async Task<IActionResult> PackingSlip(string orderId, string lines, string shipment, [FromServices] OrderDocumentBuilder documents)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null || order.Status == OrderStatus.Draft)
        {
            return NotFound();
        }

        var document = await documents.BuildPackingSlipAsync(order, OrderDocumentBuilder.ParseLines(lines), string.IsNullOrWhiteSpace(shipment) ? null : shipment.Trim());

        return View("Document", new OrderDocumentPageViewModel
        {
            Order = order,
            IsAdmin = true,
            PrintLabel = S["Print packing slip"],
            Document = await _shapeFactory.CreateAsync<OrderDocumentViewModel>("OrderPackingSlip", model => Copy(document, model)),
        });
    }

    internal static void Copy(OrderDocumentViewModel from, OrderDocumentViewModel to)
    {
        to.Order = from.Order;
        to.Header = from.Header;
        to.Title = from.Title;
        to.Placed = from.Placed;
        to.Paid = from.Paid;
        to.BilledTo = from.BilledTo;
        to.ShipsTo = from.ShipsTo;
        to.Lines = from.Lines;
        to.Discounts = from.Discounts;
        to.AmountDue = from.AmountDue;
        to.Status = from.Status;
        to.ShipmentNumber = from.ShipmentNumber;
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
    public async Task<IActionResult> Detail(
        string orderId,
        [FromServices] IDisplayManager<Order> displayManager,
        [FromServices] IUpdateModelAccessor updateModelAccessor)
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

        var model = new OrderDetailViewModel
        {
            Order = order,
            CanManage = canManage,
            Sections = await displayManager.BuildDisplayAsync(order, updateModelAccessor.ModelUpdater, OrderConstants.DisplayTypes.DetailAdmin),
        };

        if (canManage)
        {
            var operations = HttpContext.RequestServices.GetRequiredService<OrderOperationsService>();

            model.Money = await operations.GetMoneyAsync(order, HttpContext.RequestAborted);
            model.CanResendConfirmation = order.Status == OrderStatus.Paid &&
                !string.IsNullOrWhiteSpace(order.CustomerEmail) &&
                HttpContext.RequestServices.GetRequiredService<OrderConfirmationSender>().CanSend;

            // An order the store made for a buyer can be paid from this link, emailed or not, such as sent by hand.
            if (order.Invoice is not null && order.Status is OrderStatus.Draft or OrderStatus.AwaitingPayment &&
                await HttpContext.RequestServices.GetRequiredService<IOrderLinkService>().CreateUrlAsync(order) is { } viewUrl)
            {
                model.PayUrl = viewUrl + "/pay";
            }
        }

        return View(model);
    }

    [HttpPost]
    [Admin("orders/{orderId}/notes", "OrdersAddNote")]
    public async Task<IActionResult> AddNote(string orderId, string text, [FromServices] IOrderService orderService)
    {
        if (await FindManagedAsync(orderId) is not { } order)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await _notifier.WarningAsync(H["Write the note first."]);
        }
        else
        {
            await orderService.AddNoteAsync(order, text, OrderNoteKind.Note, User.Identity?.Name, HttpContext.RequestAborted);
        }

        return RedirectToAction(nameof(Detail), new { orderId });
    }

    [HttpPost]
    [Admin("orders/{orderId}/refund", "OrdersRefund")]
    public async Task<IActionResult> Refund(string orderId, OrderRefundInputModel model, [FromServices] OrderOperationsService operations)
    {
        if (await FindManagedAsync(orderId) is not { } order)
        {
            return NotFound();
        }

        var result = await operations.RefundAsync(order, model?.Amount, model?.Reason, model?.Restock == true, User.Identity?.Name, model?.AsStoreCredit == true, HttpContext.RequestAborted);

        await NotifyAsync(result, result.Refunded > 0m ? H["Refunded {0}.", ProductPriceFormatter.Format(result.Refunded, order.Currency)] : null);

        return RedirectToAction(nameof(Detail), new { orderId });
    }

    [HttpPost]
    [Admin("orders/{orderId}/cancel", "OrdersCancel")]
    public async Task<IActionResult> Cancel(string orderId, OrderCancelInputModel model, [FromServices] OrderOperationsService operations)
    {
        if (await FindManagedAsync(orderId) is not { } order)
        {
            return NotFound();
        }

        var result = await operations.CancelAsync(order, model?.Note, model?.Refund == true, model?.Restock == true, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, result.Refunded > 0m
            ? H["The order was canceled and {0} refunded.", ProductPriceFormatter.Format(result.Refunded, order.Currency)]
            : H["The order was canceled."]);

        return RedirectToAction(nameof(Detail), new { orderId });
    }

    [HttpPost]
    [Admin("orders/{orderId}/resend", "OrdersResendConfirmation")]
    public async Task<IActionResult> ResendConfirmation(string orderId, [FromServices] OrderOperationsService operations)
    {
        if (await FindManagedAsync(orderId) is not { } order)
        {
            return NotFound();
        }

        if (await operations.ResendConfirmationAsync(order, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The confirmation was sent again to {0}.", order.CustomerEmail]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The confirmation couldn't be sent. Check the site's email settings."]);
        }

        return RedirectToAction(nameof(Detail), new { orderId });
    }

    private async Task<Order> FindManagedAsync(string orderId)
    {
        if (string.IsNullOrEmpty(orderId) || !await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return null;
        }

        return await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);
    }

    private async Task NotifyAsync(OrderOperationResult result, LocalizedHtmlString success)
    {
        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        foreach (var message in result.Messages)
        {
            await _notifier.InformationAsync(H["{0}", message]);
        }

        if (result.Succeeded && success is not null)
        {
            await _notifier.SuccessAsync(success);
        }
    }

    private async Task<OrdersIndexViewModel> BuildListAsync(OrdersIndexOptions options, PagerParameters pagerParameters, string userId)
    {
        options ??= new OrdersIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var query = BuildQuery(options, userId);

        var (count, orders) = await _orderStore.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Statuses =
        [
            new SelectListItem(S["Placed"], nameof(OrderStatusFilter.Placed), options.Status == OrderStatusFilter.Placed),
            new SelectListItem(S["Paid"], nameof(OrderStatusFilter.Paid), options.Status == OrderStatusFilter.Paid),
            new SelectListItem(S["Awaiting payment"], nameof(OrderStatusFilter.AwaitingPayment), options.Status == OrderStatusFilter.AwaitingPayment),
            new SelectListItem(S["Canceled"], nameof(OrderStatusFilter.Canceled), options.Status == OrderStatusFilter.Canceled),
            new SelectListItem(S["Drafts"], nameof(OrderStatusFilter.Drafts), options.Status == OrderStatusFilter.Drafts),
            new SelectListItem(S["Any status"], nameof(OrderStatusFilter.All), options.Status == OrderStatusFilter.All),
        ];

        options.Channels =
        [
            new SelectListItem(S["Every channel"], string.Empty, string.IsNullOrEmpty(options.Channel)),
            new SelectListItem(S["Online"], OrderChannels.Online, options.Channel == OrderChannels.Online),
            new SelectListItem(S["Point of sale"], OrderChannels.PointOfSale, options.Channel == OrderChannels.PointOfSale),
            new SelectListItem(S["Drafts sent by staff"], OrderChannels.Draft, options.Channel == OrderChannels.Draft),
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

        if (!string.IsNullOrEmpty(options.Channel))
        {
            routeData.Values.TryAdd(OptionsChannel, options.Channel);
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

    // The list's search and status as a query, shared by the list and its export.
    private static OrderQuery BuildQuery(OrdersIndexOptions options, string userId)
        => new()
        {
            UserId = userId,
            Search = userId is null ? options.Search : null,
            Channel = userId is null && options.Channel is OrderChannels.Online or OrderChannels.PointOfSale or OrderChannels.Draft ? options.Channel : null,
            Statuses = userId is not null
                ? [OrderStatus.Paid, OrderStatus.AwaitingPayment]
                : options.Status switch
                {
                    OrderStatusFilter.Paid => [OrderStatus.Paid],
                    OrderStatusFilter.AwaitingPayment => [OrderStatus.AwaitingPayment],
                    OrderStatusFilter.Canceled => [OrderStatus.Canceled],
                    OrderStatusFilter.Drafts => [OrderStatus.Draft],
                    OrderStatusFilter.All => [],
                    _ => [OrderStatus.Paid, OrderStatus.AwaitingPayment],
                },
        };

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

        if (!string.IsNullOrEmpty(options?.Channel))
        {
            values.Add(OptionsChannel, options.Channel);
        }

        if (pagerParameters?.PageSize is int pageSize)
        {
            values.Add("pageSize", pageSize);
        }

        return values;
    }
}
