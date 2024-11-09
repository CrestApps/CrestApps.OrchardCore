using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubscriptionSession> _displayManager;
    private readonly IDisplayManager<ListSubscriptionOptions> _optionsDisplayManager;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

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

        var notificationShapes = new List<IShape>();

        foreach (var subscription in queryResult.Subscriptions)
        {
            var shape = await _displayManager.BuildDisplayAsync(subscription, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            shape.Properties[nameof(SubscriptionSession)] = subscription;

            notificationShapes.Add(shape);
        }

        var startIndex = (pager.Page - 1) * pager.PageSize + 1;
        options.StartIndex = startIndex;
        options.EndIndex = startIndex + notificationShapes.Count - 1;
        options.TotalSubscriptions = notificationShapes.Count;
        options.TotalItemCount = queryResult.TotalCount;

        var header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        var shapeViewModel = await shapeFactory.CreateAsync<ListSubscriptionsViewModel>("SubscriptionsAdminList", viewModel =>
        {
            viewModel.Options = options;
            viewModel.Header = header;
            viewModel.Notifications = notificationShapes;
            viewModel.Pager = pagerShape;
        });

        return View(shapeViewModel);
    }

    [Admin("manage-subscriptions/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var subscription = await _session.Query<SubscriptionSession, SubscriptionIndex>(i => i.SessionId == id).FirstOrDefaultAsync();

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
