using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Workflows;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
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
using OrchardCore.Workflows.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

[Admin]
public sealed class ActivitiesController : Controller
{
    private readonly ISession _session;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IContentManager _contentManager;
    private readonly IDisplayManager<OmnichannelActivityContainer> _containerDisplayManager;
    private readonly IDisplayManager<OmnichannelActivity> _activityDisplayManager;
    private readonly IDisplayManager<ListOmnichannelActivityFilter> _filterDisplayManager;
    private readonly IOmnichannelActivityManager _omnichannelActivityManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IWorkflowManager _workflowManager;
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public ActivitiesController(
        ISession session,
        IUpdateModelAccessor updateModelAccessor,
        IContentManager contentItemManager,
        IDisplayManager<OmnichannelActivityContainer> containerDisplayManager,
        IDisplayManager<OmnichannelActivity> activityDisplayManager,
        IDisplayManager<ListOmnichannelActivityFilter> filterDisplayManager,
        IOmnichannelActivityManager omnichannelActivityManager,
        IAuthorizationService authorizationService,
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IWorkflowManager workflowManager,
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
        _filterDisplayManager = filterDisplayManager;
        _omnichannelActivityManager = omnichannelActivityManager;
        _authorizationService = authorizationService;
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _workflowManager = workflowManager;
        _dispositionsCatalog = dispositionsCatalog;
        _clock = clock;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [Admin("omnichannel/activities", "OmnichannelActivities")]
    public async Task<IActionResult> Activities(
        ListOmnichannelActivityFilterViewModel filterViewModel,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        // Create filter from view model
        var filter = new ListOmnichannelActivityFilter
        {
            UrgencyLevel = filterViewModel.UrgencyLevel,
            SubjectContentType = filterViewModel.SubjectContentType,
            Channel = filterViewModel.Channel,
            AttemptFrom = filterViewModel.AttemptFrom,
            AttemptTo = filterViewModel.AttemptTo,
        };

        // Determine if filtering is active
        var hasFilters = filterViewModel.UrgencyLevel.HasValue ||
                        !string.IsNullOrEmpty(filterViewModel.SubjectContentType) ||
                        !string.IsNullOrEmpty(filterViewModel.Channel) ||
                        filterViewModel.AttemptFrom.HasValue ||
                        filterViewModel.AttemptTo.HasValue;

        var scheduledResult = hasFilters
            ? await _omnichannelActivityManager.PageManualScheduledAsync(userId, pager.Page, pager.PageSize, filter)
            : await _omnichannelActivityManager.PageManualScheduledAsync(userId, pager.Page, pager.PageSize);

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (filterViewModel.UrgencyLevel.HasValue)
        {
            routeData.Values.TryAdd(nameof(filterViewModel.UrgencyLevel), filterViewModel.UrgencyLevel);
        }

        if (!string.IsNullOrEmpty(filterViewModel.SubjectContentType))
        {
            routeData.Values.TryAdd(nameof(filterViewModel.SubjectContentType), filterViewModel.SubjectContentType);
        }

        if (!string.IsNullOrEmpty(filterViewModel.Channel))
        {
            routeData.Values.TryAdd(nameof(filterViewModel.Channel), filterViewModel.Channel);
        }

        if (filterViewModel.AttemptFrom.HasValue)
        {
            routeData.Values.TryAdd(nameof(filterViewModel.AttemptFrom), filterViewModel.AttemptFrom);
        }

        if (filterViewModel.AttemptTo.HasValue)
        {
            routeData.Values.TryAdd(nameof(filterViewModel.AttemptTo), filterViewModel.AttemptTo);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, scheduledResult.Count, routeData);

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

        // Build the filter header
        var header = await _filterDisplayManager.BuildEditorAsync(filter, _updateModelAccessor.ModelUpdater, isNew: false, string.Empty, string.Empty);

        var model = new ListOmnichannelActivityContainer()
        {
            Header = header,
            Containers = containerSummaries,
            Pager = pagerShape,
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Activities))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/activities", "OmnichannelActivities")]
    public ActionResult ActivitiesFilterPost(ListOmnichannelActivityFilterViewModel filterViewModel)
    {
        return RedirectToAction(nameof(Activities), new RouteValueDictionary
        {
            { nameof(filterViewModel.UrgencyLevel), filterViewModel.UrgencyLevel },
            { nameof(filterViewModel.SubjectContentType), filterViewModel.SubjectContentType },
            { nameof(filterViewModel.Channel), filterViewModel.Channel },
            { nameof(filterViewModel.AttemptFrom), filterViewModel.AttemptFrom },
            { nameof(filterViewModel.AttemptTo), filterViewModel.AttemptTo },
        });
    }

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
            ItemId = IdGenerator.GenerateId(),
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
            // Trigger the workflow here.
            activity.Subject = subject;
            activity.Status = ActivityStatus.Completed;
            activity.CompletedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            activity.CompletedByUsername = User.Identity?.Name;
            activity.CompletedUtc = _clock.UtcNow;

            await _omnichannelActivityManager.UpdateAsync(activity);
            var disposition = await _dispositionsCatalog.FindByIdAsync(activity.DispositionId);

            var input = new Dictionary<string, object>
            {
                { "Activity", activity },
                { "Contact", model.ContactContentItem },
                { "Subject", subject },
                { "Disposition", disposition },
            };

            await _workflowManager.TriggerEventAsync(nameof(CompletedActivityEvent), input, correlationId: activity.ItemId);

            await _notifier.SuccessAsync(H["The activity has been completed successfully."]);

            return RedirectToAction(nameof(Activities));
        }

        return View(model);
    }
}
