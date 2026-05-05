using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides endpoints for managing activities resources.
/// </summary>
[Admin]
public sealed class ActivitiesController : Controller
{
    private readonly ISession _session;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IContentManager _contentManager;
    private readonly IDisplayManager<OmnichannelActivityContainer> _containerDisplayManager;
    private readonly IDisplayManager<OmnichannelActivity> _activityDisplayManager;
    private readonly IOmnichannelActivityManager _omnichannelActivityManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly ICampaignActionExecutor _campaignActionExecutor;
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitiesController"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="contentItemManager">The content item manager.</param>
    /// <param name="containerDisplayManager">The container display manager.</param>
    /// <param name="activityDisplayManager">The activity display manager.</param>
    /// <param name="omnichannelActivityManager">The omnichannel activity manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentItemDisplayManager">The content item display manager.</param>
    /// <param name="campaignActionExecutor">The campaign action executor.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    public ActivitiesController(
        ISession session,
        IUpdateModelAccessor updateModelAccessor,
        IContentManager contentItemManager,
        IDisplayManager<OmnichannelActivityContainer> containerDisplayManager,
        IDisplayManager<OmnichannelActivity> activityDisplayManager,
        IOmnichannelActivityManager omnichannelActivityManager,
        IAuthorizationService authorizationService,
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        ICampaignActionExecutor campaignActionExecutor,
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IClock clock,
        ILocalClock localClock,
        INotifier notifier,
        IStringLocalizer<ActivitiesController> stringLocalizer,
        IHtmlLocalizer<ActivitiesController> htmlLocalizer)
    {
        _session = session;
        _updateModelAccessor = updateModelAccessor;
        _contentManager = contentItemManager;
        _containerDisplayManager = containerDisplayManager;
        _activityDisplayManager = activityDisplayManager;
        _omnichannelActivityManager = omnichannelActivityManager;
        _authorizationService = authorizationService;
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _campaignActionExecutor = campaignActionExecutor;
        _dispositionsCatalog = dispositionsCatalog;
        _clock = clock;
        _localClock = localClock;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Performs the activities operation.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    [Admin("omnichannel/activities", "OmnichannelActivities")]
    public async Task<IActionResult> Activities(
        ListOmnichannelActivityFilter options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] IDisplayManager<ListOmnichannelActivityFilter> filterDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        options ??= new ListOmnichannelActivityFilter();

        // Build the filter editor (this populates the filter from request parameters via the display driver)
        var header = await filterDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, isNew: false);

        var scheduledResult = await _omnichannelActivityManager.PageManualScheduledAsync(userId, pager.Page, pager.PageSize, options);

        // Maintain previous route data when generating page links.
        var pagerShape = await shapeFactory.PagerAsync(pager, scheduledResult.Count, options.RouteValues);

        var contactsIds = scheduledResult.Entries.Select(x => x.ContactContentItemId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var userIds = scheduledResult.Entries.Select(x => x.AssignedToId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var contacts = await _contentManager.GetAsync(contactsIds, VersionOptions.Latest);

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
            .ListAsync();

        var containerSummaries = new List<IShape>();

        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in scheduledResult.Entries)
        {
            var contact = contacts.FirstOrDefault(x => x.ContentItemId == entry.ContactContentItemId);

            if (!contentTypeDefinitions.TryGetValue(entry.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(entry.SubjectContentType);
                contentTypeDefinitions[entry.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(entry.SubjectContentType, entry.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == entry.AssignedToId);

            var container = new OmnichannelActivityContainer(entry, contentTypeDefinition, contact, user);

            containerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin"));
        }

        var model = new ListOmnichannelActivityContainer()
        {
            Header = header,
            Containers = containerSummaries,
            Pager = pagerShape,
        };

        return View(model);
    }

    /// <summary>
    /// Performs the activities filter post operation.
    /// </summary>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    [HttpPost]
    [ActionName(nameof(Activities))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/activities", "OmnichannelActivities")]
    public async Task<ActionResult> ActivitiesFilterPost(
        [FromServices] IDisplayManager<ListOmnichannelActivityFilter> filterDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbid();
        }

        var options = new ListOmnichannelActivityFilter();

        // Evaluate the values provided in the form post and map them to the filter result and route values.
        await filterDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, isNew: false);

        return RedirectToAction(nameof(Activities), options.RouteValues);
    }

    /// <summary>
    /// Performs the list operation.
    /// </summary>
    /// <param name="contentItemId">The content item id.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    [Admin("omnichannel/activities/{contentItemId}")]
    public async Task<IActionResult> List(
        string contentItemId,
        PagerParameters pagerParameters,
        [Bind(Prefix = "s")] PagerParameters scheduledPagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListContactActivities, contact))
        {
            return Forbid();
        }

        var scheduledPager = new Pager(scheduledPagerParameters, pagerOptions.Value.GetPageSize());

        var scheduledResults = await _omnichannelActivityManager.PageContactManualScheduledAsync(contentItemId, scheduledPager.Page, scheduledPager.PageSize);

        var scheduledPagerShape = await shapeFactory.PagerAsync(scheduledPager, scheduledResults.Count);
        scheduledPagerShape.Properties["PagerId"] = "s.pagenum";

        var completedPager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var completedResults = await _omnichannelActivityManager.PageContactManualCompletedAsync(contentItemId, scheduledPager.Page, scheduledPager.PageSize);

        var completedPagerShape = await shapeFactory.PagerAsync(completedPager, completedResults.Count);

        var userIds = scheduledResults.Entries.Select(x => x.AssignedToId)
            .Concat(completedResults.Entries.Select(x => x.CompletedById))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
            .ListAsync();

        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        var scheduledContainerSummaries = new List<IShape>();

        foreach (var scheduledActivity in scheduledResults.Entries)
        {
            if (!contentTypeDefinitions.TryGetValue(scheduledActivity.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(scheduledActivity.SubjectContentType);
                contentTypeDefinitions[scheduledActivity.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(scheduledActivity.SubjectContentType, scheduledActivity.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == scheduledActivity.AssignedToId);

            var container = new OmnichannelActivityContainer(scheduledActivity, contentTypeDefinition, contact, user);

            scheduledContainerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin", groupId: "ScheduledActivity"));
        }

        var completedContainerSummaries = new List<IShape>();

        foreach (var completedActivity in completedResults.Entries)
        {
            if (!contentTypeDefinitions.TryGetValue(completedActivity.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(completedActivity.SubjectContentType);
                contentTypeDefinitions[completedActivity.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(completedActivity.SubjectContentType, completedActivity.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == completedActivity.CompletedById);

            var container = new OmnichannelActivityContainer(completedActivity, contentTypeDefinition, contact, user);

            completedContainerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin", "CompletedActivity"));
        }

        var model = new ListOmnichannelActivity()
        {
            ContactContentItem = contact,
            ScheduledContainers = scheduledContainerSummaries,
            ScheduledPager = scheduledPagerShape,
            CompletedContainers = completedContainerSummaries,
            CompletedPager = completedPagerShape,
        };

        return View(model);
    }

    /// <summary>
    /// Creates a new .
    /// </summary>
    /// <param name="contentItemId">The content item id.</param>
    [Admin("omnichannel/activities/create/{contentItemId}")]
    public async Task<IActionResult> Create(string contentItemId)
    {
        var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.EditActivity))
        {
            return Forbid();
        }

        var activity = new OmnichannelActivity()
        {
            ItemId = UniqueId.GenerateId(),
            ContactContentItemId = contact.ContentItemId,
            ContactContentType = contact.ContentType,
            Status = ActivityStatus.NotStated,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByUsername = User.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };

        ViewData["Contact"] = contact;

        var model = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(model);
    }

    /// <summary>
    /// Creates a new post.
    /// </summary>
    /// <param name="contentItemId">The content item id.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/activities/create/{contentItemId}")]
    public async Task<IActionResult> CreatePost(string contentItemId)
    {
        var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.EditActivity))
        {
            return Forbid();
        }

        var activity = await _omnichannelActivityManager.NewAsync();

        activity.ContactContentItemId = contact.ContentItemId;
        activity.ContactContentType = contact.ContentType;
        activity.Status = ActivityStatus.NotStated;
        activity.CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
        activity.CreatedByUsername = User.Identity?.Name;
        activity.CreatedUtc = _clock.UtcNow;

        var model = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            await _omnichannelActivityManager.CreateAsync(activity);
            await _notifier.SuccessAsync(H["The activity has been created successfully."]);

            return RedirectToAction(nameof(List), new { contact.ContentItemId });
        }

        ViewData["Contact"] = contact;

        return View(model);
    }

    /// <summary>
    /// Performs the edit operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [Admin("omnichannel/activities/edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var activity = await _omnichannelActivityManager.FindByIdAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.EditActivity, activity))
        {
            return Forbid();
        }

        var subject = activity.Subject ?? await _contentManager.NewAsync(activity.SubjectContentType);

        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest),
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false),
            Subject = await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Asynchronously performs the edit operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/activities/edit/{id}")]
    public async Task<IActionResult> EditAsync(string id)
    {
        var activity = await _omnichannelActivityManager.FindByIdAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.EditActivity, activity))
        {
            return Forbid();
        }

        var subject = activity.Subject ?? await _contentManager.NewAsync(activity.SubjectContentType);

        var model = new CompleteOmnichannelActivityContainer()
        {
            Activity = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false),
            Subject = await _contentItemDisplayManager.UpdateEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            activity.Subject = subject;

            await _omnichannelActivityManager.UpdateAsync(activity);

            await _notifier.SuccessAsync(H["The activity has been updated successfully."]);

            return RedirectToAction(nameof(List), new { contentItemId = activity.ContactContentItemId });
        }

        model.ContactContentItem = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        return View(model);
    }

    /// <summary>
    /// Performs the complete operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [Admin("omnichannel/activities/complete/{id}")]
    public async Task<IActionResult> Complete(string id)
    {
        var activity = await _omnichannelActivityManager.FindByIdAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.CompleteActivity, activity))
        {
            return Forbid();
        }

        var subject = activity.Subject ?? await _contentManager.NewAsync(activity.SubjectContentType);

        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest),
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false, OmnichannelConstants.CompleteActivityGroup),
            Subject = await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Asynchronously performs the complete operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [ActionName(nameof(Complete))]
    [Admin("omnichannel/activities/complete/{id}")]
    public async Task<IActionResult> CompleteAsync(string id)
    {
        var activity = await _omnichannelActivityManager.FindByIdAsync(id);

        if (activity is null || activity.Status != ActivityStatus.NotStated)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.CompleteActivity, activity))
        {
            return Forbid();
        }

        var subject = activity.Subject ?? await _contentManager.NewAsync(activity.SubjectContentType);

        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest),
            Activity = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false, OmnichannelConstants.CompleteActivityGroup),
            Subject = await _contentItemDisplayManager.UpdateEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            // Execute campaign actions for the selected disposition.
            activity.Subject = subject;
            activity.Status = ActivityStatus.Completed;
            activity.CompletedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            activity.CompletedByUsername = User.Identity?.Name;
            activity.CompletedUtc = _clock.UtcNow;

            await _omnichannelActivityManager.UpdateAsync(activity);
            var disposition = await _dispositionsCatalog.FindByIdAsync(activity.DispositionId);

            var executionContext = new CampaignActionExecutionContext
            {
                Activity = activity,
                Contact = model.ContactContentItem,
                Subject = subject,
                Disposition = disposition,
            };

            await _campaignActionExecutor.ExecuteAsync(executionContext);

            await _notifier.SuccessAsync(H["The activity has been completed successfully."]);

            return RedirectToAction(nameof(Activities));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the bulk manage activities page.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    /// <param name="bulkActionsDisplayManager">The bulk actions display manager.</param>
    [HttpGet]
    [Admin("omnichannel/manage-activities", "ManageOmnichannelActivities")]
    public async Task<IActionResult> ManageActivities(
        BulkManageActivityFilter options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] IDisplayManager<BulkManageActivityFilter> filterDisplayManager,
        [FromServices] IDisplayManager<BulkManageOmnichannelActivityContainer> bulkActionsDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters.Page, pagerParameters.PageSize, pagerOptions.Value.GetPageSize());

        options ??= new BulkManageActivityFilter();

        var header = await filterDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, isNew: false);
        AddPagerRouteValues(options.RouteValues, pagerParameters);

        var result = await _omnichannelActivityManager.PageBulkManageableAsync(pager.Page, pager.PageSize, options);

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, options.RouteValues);

        var contactsIds = result.Entries.Select(x => x.ContactContentItemId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var userIds = result.Entries.Select(x => x.AssignedToId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var contacts = await _contentManager.GetAsync(contactsIds, VersionOptions.Latest);

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
            .ListAsync();

        var containerSummaries = new List<IShape>();
        var activityItemIds = new List<string>();
        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in result.Entries)
        {
            var contact = contacts.FirstOrDefault(x => x.ContentItemId == entry.ContactContentItemId);

            if (!contentTypeDefinitions.TryGetValue(entry.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(entry.SubjectContentType);
                contentTypeDefinitions[entry.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(entry.SubjectContentType, entry.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == entry.AssignedToId);
            var container = new OmnichannelActivityContainer(entry, contentTypeDefinition, contact, user);

            containerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin"));
            activityItemIds.Add(entry.ItemId);
        }

        var model = new BulkManageOmnichannelActivityContainer()
        {
            Header = header,
            Containers = containerSummaries,
            ActivityItemIds = activityItemIds,
            Pager = pagerShape,
            TotalCount = result.Count,
            CurrentPageSize = pager.PageSize,
            PageSizeOptions =
            [
                new SelectListItem("10", "10", pager.PageSize == 10),
                new SelectListItem("25", "25", pager.PageSize == 25),
                new SelectListItem("50", "50", pager.PageSize == 50),
                new SelectListItem("100", "100", pager.PageSize == 100),
            ],
        };

        dynamic bulkActionsShape = await bulkActionsDisplayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater);
        model.BulkActions = bulkActionsShape.Content;

        return View(model);
    }

    /// <summary>
    /// Processes a bulk action filter post for manage activities.
    /// </summary>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    [HttpPost]
    [ActionName(nameof(ManageActivities))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/manage-activities", "ManageOmnichannelActivities")]
    public async Task<ActionResult> ManageActivitiesFilterPost(
        PagerParameters pagerParameters,
        [FromServices] IDisplayManager<BulkManageActivityFilter> filterDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var options = new BulkManageActivityFilter();

        await filterDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, isNew: false);
        AddPagerRouteValues(options.RouteValues, pagerParameters);

        return RedirectToAction(nameof(ManageActivities), options.RouteValues);
    }

    /// <summary>
    /// Processes a bulk action on selected activities.
    /// </summary>
    /// <param name="viewModel">The view model containing action and selection data.</param>
    [HttpPost]
    [ActionName(nameof(ManageActivities))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("omnichannel/manage-activities", "ManageOmnichannelActivities")]
    public async Task<ActionResult> ManageActivitiesBulkActionPost(
        BulkManageActivitiesViewModel viewModel,
        PagerParameters pagerParameters,
        [FromServices] IDisplayManager<BulkManageActivityFilter> filterDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivities))
        {
            return Forbid();
        }

        var filter = new BulkManageActivityFilter();
        await filterDisplayManager.UpdateEditorAsync(filter, _updateModelAccessor.ModelUpdater, isNew: false);
        AddPagerRouteValues(filter.RouteValues, pagerParameters);
        var applyToAllMatching = Request.Form["ApplyToAllMatching"]
            .Any(value => string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase));

        if (viewModel.ItemIds is null || viewModel.ItemIds.Length == 0)
        {
            if (!applyToAllMatching)
            {
                await _notifier.WarningAsync(H["No activities were selected."]);

                return RedirectToAction(nameof(ManageActivities), filter.RouteValues);
            }
        }

        if (viewModel.BulkAction == BulkActivityAction.None)
        {
            await _notifier.WarningAsync(H["No action was selected."]);

            return RedirectToAction(nameof(ManageActivities), filter.RouteValues);
        }

        List<OmnichannelActivity> activities;

        if (applyToAllMatching)
        {
            activities = (await _omnichannelActivityManager.ListBulkManageableAsync(filter)).ToList();
        }
        else
        {
            activities = [];

            foreach (var itemId in viewModel.ItemIds)
            {
                var activity = await _omnichannelActivityManager.FindByIdAsync(itemId);

                if (activity is not null && activity.Status == ActivityStatus.NotStated && activity.InteractionType == ActivityInteractionType.Manual)
                {
                    activities.Add(activity);
                }
            }
        }

        if (activities.Count == 0)
        {
            await _notifier.WarningAsync(H["No valid activities were found for the selected action."]);

            return RedirectToAction(nameof(ManageActivities), filter.RouteValues);
        }

        var processedCount = 0;

        switch (viewModel.BulkAction)
        {
            case BulkActivityAction.Assign:
                processedCount = await BulkAssignAsync(activities, viewModel.AssignToUserIds);
                break;

            case BulkActivityAction.Reschedule:
                processedCount = await BulkRescheduleAsync(activities, viewModel.NewScheduledDate);
                break;

            case BulkActivityAction.Purge:
                processedCount = await BulkPurgeAsync(activities);
                break;

            case BulkActivityAction.SetInstructions:
                processedCount = await BulkSetInstructionsAsync(activities, viewModel.Instructions);
                break;

            case BulkActivityAction.SetUrgencyLevel:
                processedCount = await BulkSetUrgencyLevelAsync(activities, viewModel.NewUrgencyLevel);
                break;

            case BulkActivityAction.ChangeSubject:
                processedCount = await BulkChangeSubjectAsync(activities, viewModel.NewSubjectContentType);
                break;
        }

        if (processedCount > 0)
        {
            await _notifier.SuccessAsync(H["Successfully processed {0} activities.", processedCount]);
        }
        else
        {
            await _notifier.WarningAsync(H["No activities were processed. Please verify the action parameters."]);
        }

        return RedirectToAction(nameof(ManageActivities), filter.RouteValues);
    }

    private static void AddPagerRouteValues(RouteValueDictionary routeValues, PagerParameters pagerParameters)
    {
        if (pagerParameters.PageSize.HasValue)
        {
            routeValues["pageSize"] = pagerParameters.PageSize.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task<int> BulkAssignAsync(List<OmnichannelActivity> activities, string[] assignToUserIds)
    {
        if (assignToUserIds is null || assignToUserIds.Length == 0)
        {
            return 0;
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(assignToUserIds))
            .ListAsync();

        var userList = users.ToArray();

        if (userList.Length == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var processedCount = 0;

        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            var user = userList[i % userList.Length];

            activity.AssignedToId = user.UserId;
            activity.AssignedToUsername = user.UserName;
            activity.AssignedToUtc = now;

            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkRescheduleAsync(List<OmnichannelActivity> activities, string newScheduledDate)
    {
        if (string.IsNullOrEmpty(newScheduledDate))
        {
            return 0;
        }

        if (!DateTime.TryParseExact(newScheduledDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledDate) &&
            !DateTime.TryParse(newScheduledDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out scheduledDate))
        {
            return 0;
        }

        var scheduledUtc = await _localClock.ConvertToUtcAsync(scheduledDate);

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.ScheduledUtc = scheduledUtc;
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkPurgeAsync(List<OmnichannelActivity> activities)
    {
        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.Status = ActivityStatus.Purged;
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkSetInstructionsAsync(List<OmnichannelActivity> activities, string instructions)
    {
        if (instructions is null)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.Instructions = instructions;
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkSetUrgencyLevelAsync(List<OmnichannelActivity> activities, ActivityUrgencyLevel? urgencyLevel)
    {
        if (!urgencyLevel.HasValue)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.UrgencyLevel = urgencyLevel.Value;
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkChangeSubjectAsync(List<OmnichannelActivity> activities, string newSubjectContentType)
    {
        if (string.IsNullOrEmpty(newSubjectContentType))
        {
            return 0;
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(newSubjectContentType);

        if (contentTypeDefinition is null)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.SubjectContentType = newSubjectContentType;
            activity.Subject = null;
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }
}
