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
    /// <param name="workflowManager">The workflow manager.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="clock">The clock.</param>
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
}
