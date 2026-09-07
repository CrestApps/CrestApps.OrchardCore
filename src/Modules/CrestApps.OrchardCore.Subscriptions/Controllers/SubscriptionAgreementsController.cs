using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// The administration report for durable subscription agreements, and the actions an operator can take on
/// one.
/// </summary>
/// <remarks>
/// Every action here goes through the lifecycle service rather than writing the record directly. That is
/// what keeps an operator cancelling in the admin from racing a webhook or the nightly sweep, and what
/// guarantees the change is written to the agreement's audit trail.
/// </remarks>
[Admin("subscription-agreements/{action}/{itemId?}", "SubscriptionAgreements{action}")]
public sealed class SubscriptionAgreementsController : Controller
{
    private const string _optionsStatus = "Options.Status";
    private const string _optionsSearch = "Options.Search";

    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionAgreementsController"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="lifecycleService">The service that owns every subscription transition.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionAgreementsController(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<SubscriptionAgreementsController> htmlLocalizer,
        IStringLocalizer<SubscriptionAgreementsController> stringLocalizer)
    {
        _subscriptionManager = subscriptionManager;
        _lifecycleService = lifecycleService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the agreements report.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("subscription-agreements", "SubscriptionAgreementsIndex")]
    public async Task<IActionResult> Index(
        SubscriptionAgreementsIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _subscriptionManager.PageAsync(pager.Page, pager.PageSize, new SubscriptionQuery
        {
            Status = options.Status,
            Search = options.Search,
        });

        var routeData = new RouteData();

        if (options.Status.HasValue)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status.Value);
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        options.Statuses = BuildStatusItems(options.Status);

        return View(new SubscriptionAgreementsIndexViewModel
        {
            Options = options,
            Subscriptions = [.. result.Entries],
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        });
    }

    /// <summary>
    /// Preserves the report filter when the toolbar is submitted.
    /// </summary>
    /// <param name="options">The filter options.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("subscription-agreements", "SubscriptionAgreementsIndex")]
    public async Task<IActionResult> IndexFilterPost(SubscriptionAgreementsIndexOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (options.Status.HasValue)
        {
            routeValues.TryAdd(_optionsStatus, options.Status.Value);
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Displays one agreement and its history.
    /// </summary>
    /// <param name="itemId">The subscription identifier.</param>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var subscription = string.IsNullOrEmpty(itemId) ? null : await _subscriptionManager.FindByIdAsync(itemId);

        if (subscription is null)
        {
            return NotFound();
        }

        return View(subscription);
    }

    /// <summary>
    /// Ends an agreement.
    /// </summary>
    /// <param name="itemId">The subscription identifier.</param>
    /// <param name="atPeriodEnd">Whether access runs to the end of the paid period.</param>
    /// <param name="reason">The reason recorded.</param>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, bool atPeriodEnd, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var subscription = string.IsNullOrEmpty(itemId) ? null : await _subscriptionManager.FindByIdAsync(itemId);

        if (subscription is null)
        {
            return NotFound();
        }

        await _lifecycleService.CancelAsync(
            itemId,
            atPeriodEnd,
            string.IsNullOrWhiteSpace(reason) ? S["Canceled by an administrator."].Value : reason.Trim(),
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        await _notifier.SuccessAsync(atPeriodEnd
            ? H["The subscription will end when the paid period does."]
            : H["The subscription was canceled."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Suspends billing without ending an agreement.
    /// </summary>
    /// <param name="itemId">The subscription identifier.</param>
    /// <param name="reason">The reason recorded.</param>
    [HttpPost]
    public async Task<IActionResult> Pause(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var subscription = string.IsNullOrEmpty(itemId) ? null : await _subscriptionManager.FindByIdAsync(itemId);

        if (subscription is null)
        {
            return NotFound();
        }

        await _lifecycleService.PauseAsync(itemId, string.IsNullOrWhiteSpace(reason) ? S["Paused by an administrator."].Value : reason.Trim());

        await _notifier.SuccessAsync(H["Billing was suspended."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Returns a paused or past-due agreement to active.
    /// </summary>
    /// <param name="itemId">The subscription identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Resume(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var subscription = string.IsNullOrEmpty(itemId) ? null : await _subscriptionManager.FindByIdAsync(itemId);

        if (subscription is null)
        {
            return NotFound();
        }

        await _lifecycleService.ResumeAsync(itemId);

        await _notifier.SuccessAsync(H["The subscription is active again."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private List<SelectListItem> BuildStatusItems(SubscriptionStatus? selected)
    {
        var items = new List<SelectListItem>
        {
            new() { Text = S["All statuses"], Value = string.Empty, Selected = !selected.HasValue },
        };

        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            items.Add(new SelectListItem
            {
                Text = status.ToString(),
                Value = status.ToString(),
                Selected = selected == status,
            });
        }

        return items;
    }
}
