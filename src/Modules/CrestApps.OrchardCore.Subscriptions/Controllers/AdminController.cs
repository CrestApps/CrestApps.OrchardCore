using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Provides admin pages for listing and editing subscription sessions.
/// </summary>
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubscriptionSession> _displayManager;
    private readonly IDisplayManager<ListSubscriptionOptions> _optionsDisplayManager;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used to check subscription permissions.</param>
    /// <param name="updateModelAccessor">The accessor that provides the current model updater.</param>
    /// <param name="displayManager">The display manager used to build subscription session shapes.</param>
    /// <param name="optionsDisplayManager">The display manager used to build list option shapes.</param>
    /// <param name="session">The YesSql session used to query subscription sessions.</param>
    /// <param name="stringLocalizer">The string localizer for admin text.</param>
    /// <param name="htmlLocalizer">The HTML localizer for admin text.</param>
    public AdminController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubscriptionSession> displayManager,
        IDisplayManager<ListSubscriptionOptions> optionsDisplayManager,
        ISession session,
        IStringLocalizer<AdminController> stringLocalizer,
        IHtmlLocalizer<AdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _optionsDisplayManager = optionsDisplayManager;
        _session = session;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Displays the admin subscription list with filtering, sorting, and paging.
    /// </summary>
    /// <param name="queryFilterResult">The parsed subscription filter query.</param>
    /// <param name="pagerParameters">The pager parameters from the current request.</param>
    /// <param name="options">The subscription list display options.</param>
    /// <param name="queryService">The service used to query subscription sessions for the list.</param>
    /// <param name="pagerOptions">The configured pager options.</param>
    /// <param name="shapeFactory">The shape factory used to create the pager and list view model.</param>
    /// <returns>The admin subscription list view, or a forbidden result when access is denied.</returns>
    [Admin("manage-subscriptions")]
    public async Task<IActionResult> Index(
        [ModelBinder(BinderType = typeof(SubscriptionFilterEngineModelBinder), Name = "q")] QueryFilterResult<SubscriptionSession> queryFilterResult,
        PagerParameters pagerParameters,
        ListSubscriptionOptions options,
        [FromServices] ISubscriptionsAdminListQueryService queryService,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        options.FilterResult = queryFilterResult;

        // The search text is provided back to the UI.
        options.SearchText = options.FilterResult.ToString();
        options.OriginalSearchText = options.SearchText;

        // Populate route values to maintain previous route data when generating page links.
        options.RouteValues.TryAdd("q", options.FilterResult.ToString());

        options.Statuses =
        [
            new(S["Completed"], nameof(SubscriptionSessionStatus.Completed)),
            new(S["Pending"], nameof(SubscriptionSessionStatus.Pending)),
            new(S["Suspended"], nameof(SubscriptionSessionStatus.Suspended)),
        ];
        options.Sorts =
        [
            new(S["Recently created"], nameof(SubscriptionOrder.Latest)),
            new(S["Previously created"], nameof(SubscriptionOrder.Oldest)),
        ];

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var queryResult = await queryService.QueryAsync(pager.Page, pager.PageSize, options, _updateModelAccessor.ModelUpdater);

        var pagerShape = await shapeFactory.PagerAsync(pager, queryResult.TotalCount, options.RouteValues);

        var subscriptionShapes = new List<IShape>();

        foreach (var subscription in queryResult.Subscriptions)
        {
            var shape = await _displayManager.BuildDisplayAsync(subscription, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            shape.Properties[nameof(SubscriptionSession)] = subscription;

            subscriptionShapes.Add(shape);
        }

        var startIndex = (pager.Page - 1) * pager.PageSize + 1;
        options.StartIndex = startIndex;
        options.EndIndex = startIndex + subscriptionShapes.Count - 1;
        options.TotalSubscriptions = subscriptionShapes.Count;
        options.TotalItemCount = queryResult.TotalCount;

        var header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        var shapeViewModel = await shapeFactory.CreateAsync<ListSubscriptionsViewModel>("SubscriptionsAdminList", viewModel =>
        {
            viewModel.Options = options;
            viewModel.Header = header;
            viewModel.Subscriptions = subscriptionShapes;
            viewModel.Pager = pagerShape;
        });

        return View(shapeViewModel);
    }

    /// <summary>
    /// Handles the subscriptions admin list filter form post by mapping the selected status, sort, and search
    /// values into the filter query and redirecting to the list using the Post-Redirect-Get pattern.
    /// </summary>
    /// <param name="queryFilterResult">The parsed subscription filter query from the current request.</param>
    /// <param name="options">The posted subscription list options.</param>
    /// <returns>A redirect to the subscription list, or a forbidden result when access is denied.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("manage-subscriptions")]
    public async Task<IActionResult> IndexFilterPost(
        [ModelBinder(BinderType = typeof(SubscriptionFilterEngineModelBinder), Name = "q")] QueryFilterResult<SubscriptionSession> queryFilterResult,
        ListSubscriptionOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        options.FilterResult = queryFilterResult;

        if (!string.Equals(options.SearchText, options.OriginalSearchText, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new RouteValueDictionary
            {
                { "q", options.SearchText },
            });
        }

        await _optionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);
        options.RouteValues.TryAdd("q", options.FilterResult.ToString());

        return RedirectToAction(nameof(Index), options.RouteValues);
    }

    /// <summary>
    /// Displays the editor for a single subscription session.
    /// </summary>
    /// <param name="id">The subscription session identifier.</param>
    /// <returns>The subscription session editor view, a not found result, or a forbidden result.</returns>
    [Admin("manage-subscriptions/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var subscription = await _session.Query<SubscriptionSession, SubscriptionSessionIndex>(i => i.SessionId == id).FirstOrDefaultAsync();

        if (subscription == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageSubscriptions, subscription))
        {
            return Forbid();
        }

        var shape = await _displayManager.BuildEditorAsync(subscription, _updateModelAccessor.ModelUpdater, false);

        return View(shape);
    }
}
