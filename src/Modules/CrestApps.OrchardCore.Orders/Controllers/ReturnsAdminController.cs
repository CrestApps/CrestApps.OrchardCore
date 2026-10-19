using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Commerce › Returns: the buyers' requests to send items back, and what the store does with each.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
public sealed class ReturnsAdminController : Controller
{
    private readonly IOrderReturnStore _returns;
    private readonly IOrderStore _orders;
    private readonly OrderReturnService _returnService;
    private readonly OrderOperationsService _operations;
    private readonly IEnumerable<IFulfillmentLocationProvider> _locations;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly ISession _session;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public ReturnsAdminController(
        IOrderReturnStore returns,
        IOrderStore orders,
        OrderReturnService returnService,
        OrderOperationsService operations,
        IEnumerable<IFulfillmentLocationProvider> locations,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        ISession session,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<ReturnsAdminController> stringLocalizer,
        IHtmlLocalizer<ReturnsAdminController> htmlLocalizer)
    {
        _returns = returns;
        _orders = orders;
        _returnService = returnService;
        _operations = operations;
        _locations = locations;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _session = session;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [Admin("returns", "ReturnsIndex")]
    public async Task<IActionResult> Index(OrderReturnIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        options ??= new OrderReturnIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var query = new OrderReturnQuery { Search = options.Search };

        if (string.Equals(options.Show, "all", StringComparison.OrdinalIgnoreCase))
        {
            query.All = true;
        }
        else if (Enum.TryParse<OrderReturnStatus>(options.Show, ignoreCase: true, out var status))
        {
            query.Status = status;
        }

        var (count, returns) = await _returns.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Filters =
        [
            new SelectListItem(S["Open"], string.Empty, string.IsNullOrEmpty(options.Show)),
            new SelectListItem(S["Requested"], nameof(OrderReturnStatus.Requested), options.Show == nameof(OrderReturnStatus.Requested)),
            new SelectListItem(S["Approved"], nameof(OrderReturnStatus.Approved), options.Show == nameof(OrderReturnStatus.Approved)),
            new SelectListItem(S["Received"], nameof(OrderReturnStatus.Received), options.Show == nameof(OrderReturnStatus.Received)),
            new SelectListItem(S["Completed"], nameof(OrderReturnStatus.Completed), options.Show == nameof(OrderReturnStatus.Completed)),
            new SelectListItem(S["Declined"], nameof(OrderReturnStatus.Rejected), options.Show == nameof(OrderReturnStatus.Rejected)),
            new SelectListItem(S["Withdrawn"], nameof(OrderReturnStatus.Canceled), options.Show == nameof(OrderReturnStatus.Canceled)),
            new SelectListItem(S["All"], "all", options.Show == "all"),
        ];

        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            routeData.Values["Options.Search"] = options.Search;
        }

        if (!string.IsNullOrWhiteSpace(options.Show))
        {
            routeData.Values["Options.Show"] = options.Show;
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, count, routeData);

        return View(new OrderReturnIndexViewModel
        {
            Options = options,
            List = new AdminListContext<OrderReturn>
            {
                Source = returns,
                Pager = pagerShape,
                ToolbarActions = await _shapeFactory.CreateAsync("ReturnsAdminListFilters", Arguments.From(new { Options = options })),
            },
        });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("returns", "ReturnsIndex")]
    public async Task<IActionResult> IndexFilterPost(OrderReturnIndexOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var values = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(options?.Search))
        {
            values["Options.Search"] = options.Search;
        }

        if (!string.IsNullOrWhiteSpace(options?.Show))
        {
            values["Options.Show"] = options.Show;
        }

        return RedirectToAction(nameof(Index), values);
    }

    [Admin("returns/{returnId}", "ReturnsDetail")]
    public async Task<IActionResult> Detail(string returnId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var orderReturn = await FindAsync(returnId);

        if (orderReturn is null)
        {
            return NotFound();
        }

        var order = await _orders.FindByIdAsync(orderReturn.OrderId, HttpContext.RequestAborted);
        var refundable = order is null ? 0m : (await _operations.GetMoneyAsync(order, HttpContext.RequestAborted)).Refundable;
        var locations = new List<SelectListItem>();

        foreach (var provider in _locations)
        {
            locations.AddRange((await provider.GetLocationsAsync()).Select(location => new SelectListItem(location.Name, location.LocationId)));
        }

        return View(new OrderReturnDetailViewModel
        {
            Return = orderReturn,
            Order = order,
            Locations = locations,
            Refundable = refundable,
            SuggestedRefund = Math.Min(refundable, OrderReturnPolicy.GetItemsValue(orderReturn)),
            Instructions = (await _siteService.GetSettingsAsync<ReturnSettings>()).Instructions,
        });
    }

    [HttpPost]
    [Admin("returns/{returnId}/approve", "ReturnsApprove")]
    public Task<IActionResult> Approve(string returnId, string message)
        => ActAsync(returnId, async orderReturn => await _returnService.ApproveAsync(orderReturn, message, User.Identity?.Name, HttpContext.RequestAborted)
            ? H["Return {0} approved. The buyer was emailed how to send the items back.", orderReturn.Number]
            : null);

    [HttpPost]
    [Admin("returns/{returnId}/reject", "ReturnsReject")]
    public Task<IActionResult> Reject(string returnId, string message)
        => ActAsync(returnId, async orderReturn =>
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                await _notifier.ErrorAsync(H["Tell the buyer why the return is declined."]);

                return null;
            }

            return await _returnService.RejectAsync(orderReturn, message, User.Identity?.Name, HttpContext.RequestAborted)
                ? H["Return {0} declined. The buyer was told why.", orderReturn.Number]
                : null;
        });

    [HttpPost]
    [Admin("returns/{returnId}/receive", "ReturnsReceive")]
    public Task<IActionResult> Receive(string returnId, string[] restock, string locationId)
        => ActAsync(returnId, async orderReturn => await _returnService.ReceiveAsync(orderReturn, restock ?? [], locationId, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, HttpContext.RequestAborted)
            ? H["Return {0} received.", orderReturn.Number]
            : null);

    [HttpPost]
    [Admin("returns/{returnId}/refund", "ReturnsRefund")]
    public Task<IActionResult> Refund(string returnId, decimal? amount)
        => ActAsync(returnId, async orderReturn =>
        {
            if (amount is not > 0)
            {
                await _notifier.ErrorAsync(H["Enter an amount greater than zero."]);

                return null;
            }

            var problems = await _returnService.RefundAsync(orderReturn, amount.Value, User.Identity?.Name, HttpContext.RequestAborted);

            foreach (var problem in problems)
            {
                await _notifier.ErrorAsync(H["{0}", problem]);
            }

            return problems.Count == 0 ? H["Return {0} refunded, and the buyer was emailed.", orderReturn.Number] : null;
        });

    [HttpPost]
    [Admin("returns/{returnId}/exchanged", "ReturnsExchanged")]
    public Task<IActionResult> Exchanged(string returnId, string message)
        => ActAsync(returnId, async orderReturn => await _returnService.CompleteExchangeAsync(orderReturn, message, User.Identity?.Name, HttpContext.RequestAborted)
            ? H["Return {0} exchanged, and the buyer was emailed.", orderReturn.Number]
            : null);

    // Runs a step and commits it here, so a step that raced another (two staff at once) fails cleanly.
    private async Task<IActionResult> ActAsync(string returnId, Func<OrderReturn, Task<LocalizedHtmlString>> step)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var orderReturn = await FindAsync(returnId);

        if (orderReturn is null)
        {
            return NotFound();
        }

        var done = await step(orderReturn);

        if (done is null)
        {
            await _session.CancelAsync();

            if (!_notifier.List().Any())
            {
                await _notifier.WarningAsync(H["That can't be done to this return now."]);
            }

            return RedirectToAction(nameof(Detail), new { returnId });
        }

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["The return or its stock changed at the same moment. Nothing was recorded; try again."]);

            return RedirectToAction(nameof(Detail), new { returnId });
        }

        await _notifier.SuccessAsync(done);

        return RedirectToAction(nameof(Detail), new { returnId });
    }

    private async Task<OrderReturn> FindAsync(string returnId)
        => string.IsNullOrEmpty(returnId) ? null : await _returns.FindByIdAsync(returnId, HttpContext.RequestAborted);
}
