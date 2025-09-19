using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Events;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
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
    private readonly IContentManager _contentItemManager;
    private readonly IDisplayManager<OmnichannelActivityContainer> _containerDisplayManager;
    private readonly IDisplayManager<OmnichannelActivity> _activityDisplayManager;
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
        _contentItemManager = contentItemManager;
        _containerDisplayManager = containerDisplayManager;
        _activityDisplayManager = activityDisplayManager;
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

    [Admin("omnichannel/activities")]
    public async Task<IActionResult> Activities(
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.AssignedToId == userId && index.Status == ActivityStatus.NotStated && index.InteractionType == ActivityInteractionType.Manual, collection: OmnichannelConstants.CollectionName)
                    .OrderByDescending(x => x.ScheduledUtc);

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var itemsPerPage = await query.CountAsync();

        var pagerShape = await shapeFactory.PagerAsync(pager, itemsPerPage);

        var activities = await query.Skip(pager.GetStartIndex())
            .Take(pager.PageSize)
            .ListAsync();

        var contactsIds = activities.Select(x => x.ContactContentItemId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var userIds = activities.Select(x => x.AssignedToId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var contacts = await _contentItemManager.GetAsync(contactsIds, VersionOptions.Latest);

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
           .ListAsync();

        var containerSummaries = new List<IShape>();

        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var activity in activities)
        {
            var contact = contacts.FirstOrDefault(x => x.ContentItemId == activity.ContactContentItemId);

            if (!contentTypeDefinitions.TryGetValue(activity.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(activity.SubjectContentType);
                contentTypeDefinitions[activity.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(activity.SubjectContentType, activity.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == activity.AssignedToId);

            var container = new OmnichannelActivityContainer(activity, contentTypeDefinition, contact, user);

            containerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin"));
        }

        var model = new ListOmnichannelActivityContainer()
        {
            Containers = containerSummaries,
            Pager = pagerShape,
        };

        return View(model);
    }

    [Admin("omnichannel/activities/{contentItemId}")]
    public async Task<IActionResult> List(
        string contentItemId,
        PagerParameters pagerParameters,
        [Bind(Prefix = "schedule")] PagerParameters scheduledActivitiesPagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var contact = await _contentItemManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListContactActivities, contact))
        {
            return Forbid();
        }

        var scheduledActivitiesQuery = _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                        index.ContactContentItemId == contentItemId &&
                        index.Status == ActivityStatus.NotStated &&
                        index.InteractionType == ActivityInteractionType.Manual
                        , collection: OmnichannelConstants.CollectionName)
                    .OrderByDescending(x => x.ScheduledUtc);

        var scheduledPager = new Pager(scheduledActivitiesPagerParameters, pagerOptions.Value.GetPageSize());

        var scheduledPagerShape = await shapeFactory.PagerAsync(scheduledPager, await scheduledActivitiesQuery.CountAsync());

        var scheduledActivities = await scheduledActivitiesQuery
            .Skip(scheduledPager.GetStartIndex())
            .Take(scheduledPager.PageSize)
            .ListAsync();

        var completedActivitiesQuery = _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                       index.ContactContentItemId == contentItemId &&
                       index.Status == ActivityStatus.Completed
                       , collection: OmnichannelConstants.CollectionName)
                   .OrderByDescending(x => x.CompletedUtc);

        var completedPager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var completedPagerShape = await shapeFactory.PagerAsync(scheduledPager, await completedActivitiesQuery.CountAsync());

        var completedActivities = await completedActivitiesQuery
            .Skip(scheduledPager.GetStartIndex())
            .Take(scheduledPager.PageSize)
            .ListAsync();

        var userIds = scheduledActivities.Select(x => x.AssignedToId)
            .Concat(completedActivities.Select(x => x.CompletedById))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
            .ListAsync();

        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        var scheduledContainerSummaries = new List<IShape>();

        foreach (var scheduledActivity in scheduledActivities)
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

        foreach (var completedActivity in completedActivities)
        {
            if (!contentTypeDefinitions.TryGetValue(completedActivity.SubjectContentType, out var contentTypeDefinition))
            {
                contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(completedActivity.SubjectContentType);
                contentTypeDefinitions[completedActivity.SubjectContentType] = contentTypeDefinition ?? new ContentTypeDefinition(completedActivity.SubjectContentType, completedActivity.SubjectContentType);
            }

            var user = users.FirstOrDefault(x => x.UserId == completedActivity.AssignedToId);

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
        var contact = await _contentItemManager.GetAsync(contentItemId, VersionOptions.Published);

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
            Id = IdGenerator.GenerateId(),
            ContactContentItemId = contact.ContentItemId,
            ContactContentType = contact.ContentType,
            Status = ActivityStatus.NotStated,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByUsername = User.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };

        var model = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/activities/create/{contentItemId}")]
    public async Task<IActionResult> CreatePost(string contentItemId)
    {
        var contact = await _contentItemManager.GetAsync(contentItemId, VersionOptions.Published);

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
            Id = IdGenerator.GenerateId(),
            ContactContentItemId = contact.ContentItemId,
            ContactContentType = contact.ContentType,
            Status = ActivityStatus.NotStated,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByUsername = User.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };

        var model = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            await _session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName);

            await _notifier.SuccessAsync(H["The activity has been created successfully."]);

            return RedirectToAction(nameof(List), new { contact.ContentItemId });
        }

        return View(model);
    }

    [Admin("omnichannel/process/{id}")]
    public async Task<IActionResult> Process(string id)
    {
        var activity = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.ActivityId == id && index.Status == ActivityStatus.NotStated, collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync();

        if (activity is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ProcessActivity, activity))
        {
            return Forbid();
        }

        var contact = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == activity.ContactContentItemId && index.Latest).FirstOrDefaultAsync();

        var user = await _session.Query<User, UserIndex>(index => index.UserId == activity.AssignedToId).FirstOrDefaultAsync();

        var subjectContentType = await _contentDefinitionManager.GetTypeDefinitionAsync(activity.SubjectContentType);

        var subject = await _contentItemManager.NewAsync(activity.SubjectContentType);

        var model = new ProcessOmnichannelActivityContainer()
        {
            ContactContentItem = contact,
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false, groupId: string.Empty, htmlPrefix: "Activity"),
            // Contact = await _contentItemDisplayManager.BuildEditorAsync(contact, _updateModelAccessor.ModelUpdater, isNew: false, groupId: string.Empty, htmlFieldPrefix: "Contact"),
            Subject = await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true, groupId: string.Empty, htmlFieldPrefix: "Subject"),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Process))]
    [Admin("omnichannel/process/{id}")]
    public async Task<IActionResult> ProcessAsync(string id)
    {
        var activity = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.ActivityId == id && index.Status == ActivityStatus.NotStated, collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync();

        if (activity is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ProcessActivity, activity))
        {
            return Forbid();
        }

        var contact = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == activity.ContactContentItemId && index.Latest).FirstOrDefaultAsync();

        var user = await _session.Query<User, UserIndex>(index => index.UserId == activity.AssignedToId).FirstOrDefaultAsync();

        var subjectContentType = await _contentDefinitionManager.GetTypeDefinitionAsync(activity.SubjectContentType);

        var subject = await _contentItemManager.NewAsync(activity.SubjectContentType);

        var model = new ProcessOmnichannelActivityContainer()
        {
            ContactContentItem = contact,
            Activity = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false, groupId: string.Empty, htmlPrefix: "Activity"),
            // Contact = await _contentItemDisplayManager.UpdateEditorAsync(contact, _updateModelAccessor.ModelUpdater, isNew: false, groupId: string.Empty, htmlFieldPrefix: "Contact"),
            Subject = await _contentItemDisplayManager.UpdateEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true, groupId: string.Empty, htmlFieldPrefix: "Subject"),
        };

        if (ModelState.IsValid)
        {
            // Trigger the workflow here.
            activity.Subject = subject;
            activity.Status = ActivityStatus.Completed;
            activity.CompletedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            activity.CompletedByUsername = User.Identity?.Name;
            activity.CompletedUtc = _clock.UtcNow;

            await _session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName);
            // await _session.SaveAsync(contact);

            var disposition = await _dispositionsCatalog.FindByIdAsync(activity.DispositionId);

            var input = new Dictionary<string, object>
            {
                { "Activity", activity },
                { "Contact", contact },
                { "Subject", subject },
                { "Disposition", disposition },
            };

            await _workflowManager.TriggerEventAsync(nameof(CompletedActivityEvent), input, correlationId: activity.Id);

            await _notifier.SuccessAsync(H["The activity has been processed successfully."]);

            return RedirectToAction(nameof(Activities));
        }

        return View(model);
    }

    public async Task<IActionResult> LoadActivities([FromServices] IDisplayManager<OmnichannelActivityBatch> displayManager)
    {
        var model = new OmnichannelActivityBatch();

        var shape = await displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(shape);
    }
}
