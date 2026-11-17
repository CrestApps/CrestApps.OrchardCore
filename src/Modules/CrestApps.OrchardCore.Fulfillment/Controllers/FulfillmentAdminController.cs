using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Fulfillment.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Fulfillment.Controllers;

/// <summary>
/// Sends orders to fulfillment partners from an order's page, and lists what was sent under Commerce › Fulfillment
/// requests. Both need the Shipping "Ship orders" permission; only the partners' settings need Manage fulfillment partners.
/// </summary>
public sealed class FulfillmentAdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";

    private readonly IAuthorizationService _authorizationService;
    private readonly IOrderStore _orderStore;
    private readonly FulfillmentRequestStore _requestStore;
    private readonly FulfillmentSender _sender;
    private readonly FulfillmentRequestText _text;
    private readonly IEnumerable<IFulfillmentProvider> _providers;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentAdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="orderStore">The order store.</param>
    /// <param name="requestStore">The fulfillment request store.</param>
    /// <param name="sender">The fulfillment sender.</param>
    /// <param name="text">The sentences for request states.</param>
    /// <param name="providers">The fulfillment partners whose features are on.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FulfillmentAdminController(
        IAuthorizationService authorizationService,
        IOrderStore orderStore,
        FulfillmentRequestStore requestStore,
        FulfillmentSender sender,
        FulfillmentRequestText text,
        IEnumerable<IFulfillmentProvider> providers,
        INotifier notifier,
        IHtmlLocalizer<FulfillmentAdminController> htmlLocalizer,
        IStringLocalizer<FulfillmentAdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _orderStore = orderStore;
        _requestStore = requestStore;
        _sender = sender;
        _text = text;
        _providers = providers;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the orders sent, or to be sent, to fulfillment partners, most recently changed first.
    /// </summary>
    /// <param name="options">The search and status filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list page.</returns>
    [Admin("fulfillment/requests", "FulfillmentRequests")]
    public async Task<IActionResult> Index(
        FulfillmentRequestsOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        options ??= new FulfillmentRequestsOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (items, total) = await _requestStore.ListPageAsync(options.Status, options.Search, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status is { } status)
        {
            routeData.Values.TryAdd(_optionsStatus, status);
        }

        options.Statuses =
        [
            new SelectListItem(S["Any status"], string.Empty, options.Status is null),
            .. Enum.GetValues<FulfillmentRequestStatus>().Select(value => new SelectListItem(_text.Status(value), value.ToString(), options.Status == value)),
        ];

        var pagerShape = await shapeFactory.PagerAsync(pager, total, routeData);

        return View(new FulfillmentRequestsViewModel
        {
            Options = options,
            List = new AdminListContext<FulfillmentRequest>
            {
                Source = items,
                Pager = (IShape)pagerShape,
                ToolbarActions = await shapeFactory.CreateAdminListFiltersAsync(
                    "FulfillmentRequests",
                    "fulfillment-requests-filter",
                    new AdminListFilter(_optionsStatus, options.Statuses, S["Filter by status"], S["Status"])),
            },
        });
    }

    /// <summary>
    /// Applies the search and the status filter.
    /// </summary>
    /// <param name="options">The search and status filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("fulfillment/requests", "FulfillmentRequests")]
    public async Task<IActionResult> IndexFilterPost(FulfillmentRequestsOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(options?.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search.Trim());
        }

        if (options?.Status is { } status)
        {
            routeValues.TryAdd(_optionsStatus, status);
        }

        if (pagerParameters?.PageSize is { } pageSize)
        {
            routeValues.TryAdd("pageSize", pageSize);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Sends one request again, from the list page.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>A redirect to the list page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("fulfillment/requests/{requestId}/send-again", "FulfillmentSendAgain")]
    public async Task<IActionResult> SendAgain(string requestId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(requestId) || await _requestStore.FindByIdAsync(requestId, HttpContext.RequestAborted) is not { } request)
        {
            return NotFound();
        }

        var result = request.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending
            ? new FulfillmentSendResult(request)
            : await _sender.SendAsync(request, HttpContext.RequestAborted);

        await NotifyAsync(result);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Sends an order to every connected partner it was not sent to yet, and again to those that need attention.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="returnUrl">The order page to go back to.</param>
    /// <returns>A redirect to the order page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("fulfillment-partners/orders/send", "FulfillmentSendOrder")]
    public async Task<IActionResult> Send(string orderId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(orderId) || await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order)
        {
            return NotFound();
        }

        // Under the order's lock, so two people sending at once share one request per partner.
        var requests = await _sender.RecordAsync(order, lockAndCommit: true, HttpContext.RequestAborted);

        if (requests.Count == 0)
        {
            await _notifier.WarningAsync(H["No fulfillment partner is connected."]);
        }

        foreach (var request in requests.Where(request => request.Status is not (FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)))
        {
            await NotifyAsync(await _sender.SendAsync(request, HttpContext.RequestAborted));
        }

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    private async Task NotifyAsync(FulfillmentSendResult result)
    {
        var request = result.Request;
        var partner = _providers.FirstOrDefault(provider => provider.Name == request.ProviderName)?.DisplayName.Value ?? request.ProviderName;

        if (result.TakenByAnotherWorker)
        {
            await _notifier.InformationAsync(H["{0}: someone else, or the background task, is sending it right now. Refresh in a moment.", partner]);

            return;
        }

        var sentence = string.IsNullOrEmpty(request.Message)
            ? H["{0}: {1}", partner, _text.Reason(request)]
            : H["{0}: {1} The partner said: {2}", partner, _text.Reason(request), request.Message];

        switch (request.Status)
        {
            case FulfillmentRequestStatus.Sent:
                await _notifier.SuccessAsync(sentence);
                break;

            case FulfillmentRequestStatus.NeedsAttention:
                await _notifier.ErrorAsync(sentence);
                break;

            default:
                await _notifier.InformationAsync(sentence);
                break;
        }
    }
}
