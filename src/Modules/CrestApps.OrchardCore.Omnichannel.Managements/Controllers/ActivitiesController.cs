using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Lists.Indexes;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users;
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
    private readonly IActivityDispositionService _activityDispositionService;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IEnumerable<IActivityDialerContributor> _dialerContributors;

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
    /// <param name="activityDispositionService">The activity disposition service.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProvider">The user display name provider.</param>
    /// <param name="dialerContributors">The optional dialer contributors.</param>
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
        IActivityDispositionService activityDispositionService,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IClock clock,
        ILocalClock localClock,
        INotifier notifier,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IEnumerable<IActivityDialerContributor> dialerContributors,
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
        _activityDispositionService = activityDispositionService;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _clock = clock;
        _localClock = localClock;
        _notifier = notifier;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _dialerContributors = dialerContributors;
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

        var pager = new Pager(pagerParameters, pagerOptions.Value);

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

            var contentTypeDefinition = await GetSubjectContentTypeDefinitionAsync(entry, contentTypeDefinitions);

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
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    [HttpPost]
    [ActionName(nameof(Activities))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/activities", "OmnichannelActivities")]
    public async Task<ActionResult> ActivitiesFilterPost(
        PagerParameters pagerParameters,
        [FromServices] IDisplayManager<ListOmnichannelActivityFilter> filterDisplayManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbid();
        }

        var options = new ListOmnichannelActivityFilter();

        // Evaluate the values provided in the form post and map them to the filter result and route values.
        await filterDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, isNew: false);
        AddPagerRouteValues(options.RouteValues, pagerParameters);

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

        var scheduledPager = new Pager(WithSharedPageSize(scheduledPagerParameters, pagerParameters), pagerOptions.Value);

        var scheduledResults = await _omnichannelActivityManager.PageContactManualScheduledAsync(contentItemId, scheduledPager.Page, scheduledPager.PageSize);

        var scheduledPagerShape = await shapeFactory.PagerAsync(scheduledPager, scheduledResults.Count);
        scheduledPagerShape.Properties["PagerId"] = "s.pagenum";

        var completedPager = new Pager(pagerParameters, pagerOptions.Value);

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
            var contentTypeDefinition = await GetSubjectContentTypeDefinitionAsync(scheduledActivity, contentTypeDefinitions);

            var user = users.FirstOrDefault(x => x.UserId == scheduledActivity.AssignedToId);

            var container = new OmnichannelActivityContainer(scheduledActivity, contentTypeDefinition, contact, user);

            scheduledContainerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin", groupId: "ScheduledActivity"));
        }

        var completedContainerSummaries = new List<IShape>();

        foreach (var completedActivity in completedResults.Entries)
        {
            var contentTypeDefinition = await GetSubjectContentTypeDefinitionAsync(completedActivity, contentTypeDefinitions);

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
    /// Lists the scheduled and completed activities of every contact in an account.
    /// </summary>
    /// <param name="contentItemId">The account content item id.</param>
    /// <param name="pagerParameters">The pager of the completed activities.</param>
    /// <param name="scheduledPagerParameters">The pager of the scheduled activities.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("omnichannel/accounts/{contentItemId}/activities", "OmnichannelAccountActivities")]
    public async Task<IActionResult> Account(
        string contentItemId,
        PagerParameters pagerParameters,
        [Bind(Prefix = "s")] PagerParameters scheduledPagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var account = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (account is null || !account.Has<AccountPart>())
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ListContactActivities, account))
        {
            return Forbid();
        }

        // An account holds its contacts through the list part, so its activities are those of the items it contains.
        var memberIds = (await _session.QueryIndex<ContainedPartIndex>(index => index.ListContentItemId == contentItemId)
            .ListAsync())
            .Select(index => index.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var scheduledPager = new Pager(WithSharedPageSize(scheduledPagerParameters, pagerParameters), pagerOptions.Value);
        var completedPager = new Pager(pagerParameters, pagerOptions.Value);

        var scheduled = await PageAccountActivitiesAsync(memberIds, completed: false, scheduledPager);
        var completed = await PageAccountActivitiesAsync(memberIds, completed: true, completedPager);

        var scheduledPagerShape = await shapeFactory.PagerAsync(scheduledPager, scheduled.Count);
        scheduledPagerShape.Properties["PagerId"] = "s.pagenum";
        var completedPagerShape = await shapeFactory.PagerAsync(completedPager, completed.Count);

        var userIds = scheduled.Entries.Select(x => x.AssignedToId)
            .Concat(completed.Entries.Select(x => x.CompletedById))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var users = userIds.Length == 0
            ? []
            : (await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds)).ListAsync()).ToArray();

        var contactIds = scheduled.Entries.Concat(completed.Entries)
            .Select(activity => activity.ContactContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var contacts = (await _contentManager.GetAsync(contactIds, VersionOptions.Latest))
            .ToDictionary(contact => contact.ContentItemId, StringComparer.Ordinal);

        var contentTypeDefinitions = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        var model = new ListAccountActivitiesViewModel
        {
            Account = account,
            ScheduledPager = scheduledPagerShape,
            CompletedPager = completedPagerShape,
        };

        foreach (var activity in scheduled.Entries)
        {
            contacts.TryGetValue(activity.ContactContentItemId ?? string.Empty, out var contact);

            var container = new OmnichannelActivityContainer(
                activity,
                await GetSubjectContentTypeDefinitionAsync(activity, contentTypeDefinitions),
                contact,
                users.FirstOrDefault(x => x.UserId == activity.AssignedToId));

            model.Scheduled.Add(new AccountActivityEntry
            {
                Contact = contact,
                Shape = await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin", groupId: "ScheduledActivity"),
            });
        }

        foreach (var activity in completed.Entries)
        {
            contacts.TryGetValue(activity.ContactContentItemId ?? string.Empty, out var contact);

            var container = new OmnichannelActivityContainer(
                activity,
                await GetSubjectContentTypeDefinitionAsync(activity, contentTypeDefinitions),
                contact,
                users.FirstOrDefault(x => x.UserId == activity.CompletedById));

            model.Completed.Add(new AccountActivityEntry
            {
                Contact = contact,
                Shape = await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin", "CompletedActivity"),
            });
        }

        return View(model);
    }

    private static bool IsConvertedLead(ContentItem contact)
        => contact.TryGet<LeadPart>(out var leadPart) && leadPart.IsConverted;

    private async Task<PageResult<OmnichannelActivity>> PageAccountActivitiesAsync(string[] contactIds, bool completed, Pager pager)
    {
        if (contactIds.Length == 0)
        {
            return new PageResult<OmnichannelActivity> { Count = 0, Entries = [] };
        }

        var query = completed
            ? _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                index.ContactContentItemId.IsIn(contactIds) &&
                index.Status == ActivityStatus.Completed,
                collection: OmnichannelConstants.CollectionName)
                .OrderByDescending(index => index.CompletedUtc)
                .ThenBy(index => index.Id)
            : _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                index.ContactContentItemId.IsIn(contactIds) &&
                index.Status == ActivityStatus.NotStated &&
                index.InteractionType == ActivityInteractionType.Manual,
                collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.ScheduledUtc)
                .ThenBy(index => index.Id);

        var skip = (Math.Max(pager.Page, 1) - 1) * pager.PageSize;

        return new PageResult<OmnichannelActivity>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pager.PageSize).ListAsync()).ToArray(),
        };
    }

    /// <summary>
    /// Creates a new outbound (scheduled) activity.
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

        if (IsConvertedLead(contact))
        {
            await _notifier.WarningAsync(H["This lead was converted, so new activities go on the contact it became."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
        }

        var outboundSubjects = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync(SubjectDirection.Outbound);

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
        ViewData["HasOutboundSubjects"] = outboundSubjects.Count > 0;

        var model = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(model);
    }

    /// <summary>
    /// Creates a new outbound (scheduled) activity.
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

        if (IsConvertedLead(contact))
        {
            await _notifier.WarningAsync(H["This lead was converted, so new activities go on the contact it became."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
        }

        var outboundSubjects = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync(SubjectDirection.Outbound);

        if (outboundSubjects.Count == 0)
        {
            await _notifier.WarningAsync(H["There is no configured outbound subject yet. You can't create a scheduled activity."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
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
        ViewData["HasOutboundSubjects"] = true;

        return View(model);
    }

    /// <summary>
    /// Logs a completed inbound activity for the contact.
    /// </summary>
    /// <param name="contentItemId">The contact content item id.</param>
    /// <param name="subjectContentType">The optional selected inbound subject content type.</param>
    [Admin("omnichannel/activities/create-inbound/{contentItemId}")]
    public async Task<IActionResult> CreateInbound(string contentItemId, string subjectContentType)
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

        if (IsConvertedLead(contact))
        {
            await _notifier.WarningAsync(H["This lead was converted, so new activities go on the contact it became."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
        }

        var inboundSubjects = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync(SubjectDirection.Inbound);

        var model = new CreateInboundActivityViewModel
        {
            ContactContentItem = contact,
            HasInboundSubjects = inboundSubjects.Count > 0,
            SubjectContentTypes = inboundSubjects.Select(x => new SelectListItem(x.DisplayName, x.Name)).ToArray(),
        };

        if (!model.HasInboundSubjects)
        {
            return View(model);
        }

        var selectedSubjectContentType = ResolveInboundSubjectContentType(subjectContentType, inboundSubjects);
        model.SubjectContentType = selectedSubjectContentType;

        var activity = await BuildInboundActivityAsync(contact, selectedSubjectContentType);

        var subject = string.IsNullOrEmpty(selectedSubjectContentType)
            ? null
            : await _contentManager.NewAsync(selectedSubjectContentType);

        model.Subject = subject is null
            ? null
            : await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true);

        model.Container = new CompleteOmnichannelActivityContainer
        {
            ContactContentItem = contact,
            Contact = await BuildContactInformationAsync(contact),
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true, OmnichannelConstants.CompleteActivityGroup),
        };

        return View(model);
    }

    /// <summary>
    /// Logs a completed inbound activity for the contact.
    /// </summary>
    /// <param name="contentItemId">The contact content item id.</param>
    /// <param name="subjectContentType">The selected inbound subject content type.</param>
    /// <param name="actionScheduleDates">The subject-action schedule dates submitted with the disposition.</param>
    /// <param name="actionPreparationNotes">The subject-action preparation notes submitted with the disposition.</param>
    [HttpPost]
    [ActionName(nameof(CreateInbound))]
    [Admin("omnichannel/activities/create-inbound/{contentItemId}")]
    public async Task<IActionResult> CreateInboundPost(
        string contentItemId,
        string subjectContentType,
        [Bind(Prefix = "ActionScheduleDates")] Dictionary<string, DateTime?> actionScheduleDates,
        [Bind(Prefix = "ActionPreparationNotes")] Dictionary<string, string> actionPreparationNotes)
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

        if (IsConvertedLead(contact))
        {
            await _notifier.WarningAsync(H["This lead was converted, so new activities go on the contact it became."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
        }

        var inboundSubjects = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync(SubjectDirection.Inbound);

        if (inboundSubjects.Count == 0)
        {
            await _notifier.WarningAsync(H["There is no configured inbound subject yet. You can't log an inbound activity."]);

            return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
        }

        var model = new CreateInboundActivityViewModel
        {
            ContactContentItem = contact,
            HasInboundSubjects = true,
            SubjectContentTypes = inboundSubjects.Select(x => new SelectListItem(x.DisplayName, x.Name)).ToArray(),
        };

        var isValidSubject = !string.IsNullOrEmpty(subjectContentType) &&
            inboundSubjects.Any(x => string.Equals(x.Name, subjectContentType, StringComparison.Ordinal));

        if (!isValidSubject)
        {
            ModelState.AddModelError(nameof(model.SubjectContentType), S["Select an inbound subject before logging the activity."]);
        }

        var selectedSubjectContentType = isValidSubject
            ? subjectContentType
            : ResolveInboundSubjectContentType(subjectContentType, inboundSubjects);
        model.SubjectContentType = selectedSubjectContentType;

        var activity = await BuildInboundActivityAsync(contact, selectedSubjectContentType);

        var subject = string.IsNullOrEmpty(selectedSubjectContentType)
            ? null
            : await _contentManager.NewAsync(selectedSubjectContentType);

        var subjectEditor = subject is null
            ? null
            : await _contentItemDisplayManager.UpdateEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: true);
        var activityEditor = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: true, OmnichannelConstants.CompleteActivityGroup);

        if (ModelState.IsValid)
        {
            if (subject is not null)
            {
                activity.Subject = subject;
            }

            await _omnichannelActivityManager.CreateAsync(activity);

            var result = await _activityDispositionService.ApplyAsync(new ActivityDispositionRequest
            {
                Activity = activity,
                DispositionId = activity.DispositionId,
                ActionScheduleDates = actionScheduleDates,
                ActionPreparationNotes = actionPreparationNotes,
                Source = ActivityDispositionSource.Agent,
                ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ActorDisplayName = await GetCurrentUserDisplayNameAsync(),
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The inbound activity has been logged successfully."]);

                return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
            }

            await _notifier.ErrorAsync(H["A disposition is required to complete this activity."]);
        }

        model.Subject = subjectEditor;
        model.Container = new CompleteOmnichannelActivityContainer
        {
            ContactContentItem = contact,
            Contact = await BuildContactInformationAsync(contact),
            Activity = activityEditor,
        };

        return View(model);
    }

    private static string ResolveInboundSubjectContentType(string requested, IReadOnlyList<ContentTypeDefinition> inboundSubjects)
    {
        if (!string.IsNullOrEmpty(requested) &&
            inboundSubjects.Any(subject => string.Equals(subject.Name, requested, StringComparison.Ordinal)))
        {
            return requested;
        }

        return inboundSubjects.Count == 1
            ? inboundSubjects[0].Name
            : null;
    }

    private async Task<OmnichannelActivity> BuildInboundActivityAsync(ContentItem contact, string subjectContentType)
    {
        var now = _clock.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        var activity = await _omnichannelActivityManager.NewAsync();

        activity.ContactContentItemId = contact.ContentItemId;
        activity.ContactContentType = contact.ContentType;
        activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;
        activity.Source = ActivitySources.Inbound;
        activity.Status = ActivityStatus.NotStated;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.CreatedById = userId;
        activity.CreatedByUsername = username;
        activity.CreatedUtc = now;
        activity.AssignedToId = userId;
        activity.AssignedToUsername = username;
        activity.AssignedToUtc = now;
        activity.ScheduledUtc = now;

        if (!string.IsNullOrEmpty(subjectContentType))
        {
            activity.SubjectContentType = subjectContentType;

            var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(subjectContentType);

            if (flowSettings is not null)
            {
                activity.CampaignId = flowSettings.CampaignId;
                activity.Channel = flowSettings.Channel;
                activity.InteractionType = flowSettings.InteractionType;
                activity.ChannelEndpointId = flowSettings.ChannelEndpointId;

                if (!string.IsNullOrEmpty(flowSettings.Channel))
                {
                    activity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(contact, flowSettings.Channel);
                }
            }
        }

        return activity;
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

        var subject = activity.Subject;

        if (subject is null && !string.IsNullOrEmpty(activity.SubjectContentType))
        {
            subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest),
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false),
            Subject = subject is null
                ? null
                : await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: false),
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

        var subject = activity.Subject;

        if (subject is null && !string.IsNullOrEmpty(activity.SubjectContentType))
        {
            subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        var model = new CompleteOmnichannelActivityContainer()
        {
            Activity = await _activityDisplayManager.UpdateEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false),
            Subject = subject is null
                ? null
                : await _contentItemDisplayManager.UpdateEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            if (subject is not null)
            {
                activity.Subject = subject;
            }

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
    /// <param name="returnUrl">The optional local URL used after completing or cancelling the activity.</param>
    [Admin("omnichannel/activities/complete/{id}")]
    public async Task<IActionResult> Complete(string id, string returnUrl)
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

        // Asking for a disposition on an activity that is already finished only sets the agent up to lose their
        // work, because the submit cannot be accepted.
        if (IsAlreadyFinished(activity))
        {
            return await AlreadyFinishedAsync(returnUrl);
        }

        var subject = activity.Subject;

        if (subject is null && !string.IsNullOrEmpty(activity.SubjectContentType))
        {
            subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);
        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = contact,
            Contact = contact is null
                ? null
                : await BuildContactInformationAsync(contact),
            Activity = await _activityDisplayManager.BuildEditorAsync(activity, _updateModelAccessor.ModelUpdater, isNew: false, OmnichannelConstants.CompleteActivityGroup),
            Subject = subject is null
                ? null
                : await _contentItemDisplayManager.BuildEditorAsync(subject, _updateModelAccessor.ModelUpdater, isNew: false),
            ReturnUrl = GetSafeReturnUrl(returnUrl),
        };

        return View(model);
    }

    /// <summary>
    /// Asynchronously performs the complete operation.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="returnUrl">The optional local URL used after completing the activity.</param>
    /// <param name="actionScheduleDates">The subject-action schedule dates submitted with the disposition.</param>
    /// <param name="actionPreparationNotes">The subject-action preparation notes submitted with the disposition.</param>
    [HttpPost]
    [ActionName(nameof(Complete))]
    [Admin("omnichannel/activities/complete/{id}")]
    public async Task<IActionResult> CompleteAsync(
        string id,
        string returnUrl,
        [Bind(Prefix = "ActionScheduleDates")] Dictionary<string, DateTime?> actionScheduleDates,
        [Bind(Prefix = "ActionPreparationNotes")] Dictionary<string, string> actionPreparationNotes)
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

        // An activity that is already finished cannot be completed again, but "already done" is not "never
        // existed": answering 404 gave the agent an error page and threw away the disposition, the notes and the
        // scheduled actions they had just filled in, with nothing saying why.
        if (IsAlreadyFinished(activity))
        {
            return await AlreadyFinishedAsync(returnUrl);
        }

        var subject = activity.Subject;

        if (subject is null && !string.IsNullOrEmpty(activity.SubjectContentType))
        {
            subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        var activityEditor = await _activityDisplayManager.UpdateEditorAsync(
            activity,
            _updateModelAccessor.ModelUpdater,
            isNew: false,
            OmnichannelConstants.CompleteActivityGroup);
        var subjectEditor = subject is null
            ? null
            : await _contentItemDisplayManager.UpdateEditorAsync(
                subject,
                _updateModelAccessor.ModelUpdater,
                isNew: false);
        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        var model = new CompleteOmnichannelActivityContainer()
        {
            ContactContentItem = contact,
            Contact = contact is null
                ? null
                : await BuildContactInformationAsync(contact),
            Activity = activityEditor,
            Subject = subjectEditor,
            ReturnUrl = GetSafeReturnUrl(returnUrl),
        };

        if (ModelState.IsValid)
        {
            // Disposition the activity through the source-neutral path so the configured subject flow runs.
            if (subject is not null)
            {
                activity.Subject = subject;
            }

            var result = await _activityDispositionService.ApplyAsync(new ActivityDispositionRequest
            {
                Activity = activity,
                DispositionId = activity.DispositionId,
                ActionScheduleDates = actionScheduleDates,
                ActionPreparationNotes = actionPreparationNotes,
                Source = ActivityDispositionSource.Agent,
                ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ActorDisplayName = await GetCurrentUserDisplayNameAsync(),
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The activity has been completed successfully."]);

                if (!string.IsNullOrEmpty(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction(nameof(Activities));
            }

            await _notifier.ErrorAsync(H["A disposition is required to complete this activity."]);
        }

        return View(model);
    }

    /// <summary>
    /// Builds the contact information card shown on the activity pages. The card shows who the contact is, so a
    /// list part on the contact type does not render the items listed under the contact there.
    /// </summary>
    private async Task<IShape> BuildContactInformationAsync(ContentItem contact)
    {
        var shape = await _contentItemDisplayManager.BuildDisplayAsync(contact, _updateModelAccessor.ModelUpdater, "Detail");

        RemoveListPartShapes(shape);

        return shape;
    }

    internal static void RemoveListPartShapes(IShape contactShape)
    {
        if (contactShape is not IZoneHolding zoneHolding ||
            zoneHolding.Zones["Content"] is not Shape content ||
            !content.HasItems)
        {
            return;
        }

        var listPartShapes = content.Items
            .OfType<IShape>()
            .Where(shape => shape.Metadata.Type == ContactListPartShapeTableProvider.ListPartName)
            .ToArray();

        foreach (var listPartShape in listPartShapes)
        {
            content.Remove(listPartShape.Metadata.Name);
        }
    }

    private string GetSafeReturnUrl(string returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
    }

    /// <summary>
    /// Whether the activity has already reached an end state, so there is no outcome left to record.
    /// </summary>
    private static bool IsAlreadyFinished(OmnichannelActivity activity)
        => activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged;

    /// <summary>
    /// Sends the agent back where they came from, told why the activity would not take their disposition.
    /// </summary>
    /// <remarks>
    /// This happens in ordinary use rather than only through a stale link: an automated call that hands off to a
    /// live agent concludes its own activity, so an agent who wraps up afterwards is dispositioning something the
    /// automation has already closed.
    /// </remarks>
    private async Task<IActionResult> AlreadyFinishedAsync(string returnUrl)
    {
        await _notifier.WarningAsync(H["This activity was already completed, so your disposition was not recorded."]);

        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        return string.IsNullOrEmpty(safeReturnUrl)
            ? RedirectToAction(nameof(Activities))
            : Redirect(safeReturnUrl);
    }

    /// <summary>
    /// Purges a scheduled activity from a contact profile.
    /// </summary>
    /// <param name="contentItemId">The contact content item identifier.</param>
    /// <param name="id">The activity identifier.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("omnichannel/activities/purge/{contentItemId}/{id}")]
    public async Task<IActionResult> Purge(string contentItemId, string id)
    {
        var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return NotFound();
        }

        var activity = await _omnichannelActivityManager.FindByIdAsync(id);

        if (!IsContactScheduledActivity(activity, contact.ContentItemId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.PurgeActivity, activity))
        {
            return Forbid();
        }

        var purgedAtUtc = _clock.UtcNow;
        var purgedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var purgedByUsername = User.Identity?.Name;

        ActivityPurgeHelper.Purge(
            activity,
            purgedAtUtc,
            purgedById,
            purgedByUsername);
        await _omnichannelActivityManager.UpdateAsync(activity);
        await _notifier.SuccessAsync(H["The activity has been purged successfully."]);

        return RedirectToAction(nameof(List), new { contentItemId = contact.ContentItemId });
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

        var pager = new Pager(pagerParameters, pagerOptions.Value);

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

            var contentTypeDefinition = await GetSubjectContentTypeDefinitionAsync(entry, contentTypeDefinitions);

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
        };

        dynamic bulkActionsShape = await bulkActionsDisplayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater);
        model.BulkActions = bulkActionsShape.Content;

        return View(model);
    }

    private async Task<ContentTypeDefinition> GetSubjectContentTypeDefinitionAsync(
        OmnichannelActivity activity,
        Dictionary<string, ContentTypeDefinition> contentTypeDefinitions)
    {
        var subjectContentType = activity.SubjectContentType;

        if (string.IsNullOrEmpty(subjectContentType))
        {
            return new ContentTypeDefinition(nameof(OmnichannelActivity), activity.Kind.ToString());
        }

        if (!contentTypeDefinitions.TryGetValue(subjectContentType, out var contentTypeDefinition))
        {
            contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType)
                ?? new ContentTypeDefinition(subjectContentType, subjectContentType);
            contentTypeDefinitions[subjectContentType] = contentTypeDefinition;
        }

        return contentTypeDefinition;
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
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    /// <param name="activitySourceOptions">The activity sources registered by the enabled features.</param>
    [HttpPost]
    [ActionName(nameof(ManageActivities))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("omnichannel/manage-activities", "ManageOmnichannelActivities")]
    public async Task<ActionResult> ManageActivitiesBulkActionPost(
        BulkManageActivitiesViewModel viewModel,
        PagerParameters pagerParameters,
        [FromServices] IDisplayManager<BulkManageActivityFilter> filterDisplayManager,
        [FromServices] IOptions<ActivitySourceOptions> activitySourceOptions)
    {
        var requiredPermission = viewModel.BulkAction == BulkActivityAction.Purge
            ? OmnichannelConstants.Permissions.PurgeActivity
            : OmnichannelConstants.Permissions.ManageActivities;

        if (!await _authorizationService.AuthorizeAsync(User, requiredPermission))
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

        ActivitySourceEntry newSourceEntry = null;

        // Only the sources no other feature owns may be set by hand. A dialer mode is set through the dialer
        // profile, and a value that is not registered at all would leave activities no screen can find again.
        if (viewModel.BulkAction == BulkActivityAction.ChangeSource &&
            !activitySourceOptions.Value.TryGetManuallyAssignableSource(viewModel.NewSource, out newSourceEntry))
        {
            await _notifier.WarningAsync(H["The selected source cannot be set by hand. Choose one of the listed sources."]);

            return RedirectToAction(nameof(ManageActivities), filter.RouteValues);
        }

        List<OmnichannelActivity> activities;

        if (applyToAllMatching)
        {
            activities = (await _omnichannelActivityManager.GetBulkManageableAsync(filter)).ToList();
        }
        else
        {
            activities = [];

            foreach (var itemId in viewModel.ItemIds)
            {
                var activity = await _omnichannelActivityManager.FindByIdAsync(itemId);

                if (IsBulkManageableActivity(activity))
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
                var purgedAtUtc = _clock.UtcNow;
                var purgedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var purgedByUsername = User.Identity?.Name;

                processedCount = await BulkPurgeAsync(
                    activities,
                    purgedAtUtc,
                    purgedById,
                    purgedByUsername);
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

            case BulkActivityAction.ClearAssignment:
                processedCount = await BulkClearAssignmentAsync(activities);
                break;

            case BulkActivityAction.ChangeSource:
                processedCount = await BulkChangeSourceAsync(activities, newSourceEntry.Source, viewModel.NewInteractionType, viewModel.ClearCurrentAssignment);
                break;

            case BulkActivityAction.ChangeDialerProfile:
                processedCount = await BulkChangeDialerProfileAsync(activities, viewModel.NewDialerProfileId);
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

    // A page that shows two paged lists renders a page size selector under each, and both write the one
    // unprefixed "pageSize" query value, so the prefixed list takes its page number alone and shares the page size.
    private static PagerParameters WithSharedPageSize(PagerParameters prefixedPagerParameters, PagerParameters pagerParameters)
        => new()
        {
            Page = prefixedPagerParameters.Page,
            PageSize = pagerParameters.PageSize,
        };

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
            activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
            ClearReservationState(activity);
            ApplyInitialStatus(activity, hasAssignedUser: true);

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

    private async Task<int> BulkPurgeAsync(
        List<OmnichannelActivity> activities,
        DateTime purgedAtUtc,
        string purgedById,
        string purgedByUsername)
    {
        var processedCount = 0;

        foreach (var activity in activities)
        {
            ActivityPurgeHelper.Purge(activity, purgedAtUtc, purgedById, purgedByUsername);
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

        var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(newSubjectContentType);

        if (flowSettings is null)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.SubjectContentType = newSubjectContentType;
            activity.CampaignId = flowSettings.CampaignId;
            activity.Channel = flowSettings.Channel;
            activity.InteractionType = flowSettings.InteractionType;
            activity.ChannelEndpointId = flowSettings.ChannelEndpointId;
            activity.Subject = null;
            activity.Source = ResolveSourceForChangedSubject(activity.Source, flowSettings.InteractionType);

            var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null)
            {
                activity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(contact, flowSettings.Channel);
            }

            ApplyInitialStatus(activity, hasAssignedUser: !string.IsNullOrEmpty(activity.AssignedToId));

            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkClearAssignmentAsync(List<OmnichannelActivity> activities)
    {
        var processedCount = 0;

        foreach (var activity in activities)
        {
            ResetAssignment(activity);
            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<int> BulkChangeSourceAsync(
        List<OmnichannelActivity> activities,
        string newSource,
        ActivityInteractionType? newInteractionType,
        bool clearCurrentAssignment)
    {
        if (string.IsNullOrWhiteSpace(newSource))
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var activity in activities)
        {
            activity.Source = newSource.Trim();

            if (newInteractionType.HasValue)
            {
                activity.InteractionType = newInteractionType.Value;

                if (activity.InteractionType == ActivityInteractionType.Manual)
                {
                    activity.AISessionId = null;
                }
            }

            if (clearCurrentAssignment)
            {
                ResetAssignment(activity);
            }
            else if (string.IsNullOrEmpty(activity.AssignedToId))
            {
                activity.AssignmentStatus = ActivityAssignmentStatus.Available;
                ApplyInitialStatus(activity, hasAssignedUser: false);
            }

            await _omnichannelActivityManager.UpdateAsync(activity);
            processedCount++;
        }

        return processedCount;
    }

    internal async Task<int> BulkChangeDialerProfileAsync(
        List<OmnichannelActivity> activities,
        string dialerProfileId)
    {
        if (string.IsNullOrWhiteSpace(dialerProfileId))
        {
            return 0;
        }

        var dialerContributor = _dialerContributors.FirstOrDefault();

        if (dialerContributor is null)
        {
            return 0;
        }

        var profile = await dialerContributor.FindByIdAsync(dialerProfileId);

        if (profile is null)
        {
            return 0;
        }

        // Dialer work waits on its campaign's queue, which is what agents sign in to, so a record without a campaign
        // cannot be dialed. It used to be relabeled with the dialer's source anyway and then sat where nothing found it.
        var withoutCampaignCount = activities.Count(activity => string.IsNullOrWhiteSpace(activity.CampaignId));

        if (withoutCampaignCount > 0)
        {
            await _notifier.WarningAsync(H["{0} activities have no campaign, so they cannot be dialed and were left unchanged. Set their campaign first.", withoutCampaignCount]);
        }

        var processedCount = 0;

        foreach (var campaignActivities in activities
            .Where(activity => !string.IsNullOrWhiteSpace(activity.CampaignId))
            .GroupBy(activity => activity.CampaignId, StringComparer.Ordinal))
        {
            var activityIds = campaignActivities
                .Select(activity => activity.ItemId)
                .ToHashSet(StringComparer.Ordinal);

            var conflicts = await DialerCampaignProfileGuard.FindConflictsAsync(
                dialerContributor,
                campaignActivities.Key,
                profile.ProfileId,
                activityIds,
                HttpContext.RequestAborted);

            if (conflicts.Count > 0)
            {
                await _notifier.WarningAsync(H["{0} activities were left unchanged: their campaign already has records waiting under {1}. A campaign's waiting records must all use one dialer profile, so include those records in the change or move these to another campaign.", activityIds.Count, DialerCampaignProfileGuard.Describe(conflicts)]);

                continue;
            }

            foreach (var activityId in activityIds)
            {
                // Queueing a record commits the unit of work, and a committed session no longer tracks what it loaded
                // before, so saving an activity read earlier would store a second copy. Each is read as the last commit left it.
                var activity = await _omnichannelActivityManager.FindByIdAsync(activityId);

                if (!IsBulkManageableActivity(activity))
                {
                    continue;
                }

                // The profile no longer owns a campaign; changing the dialer profile keeps the activity's own campaign.
                activity.Source = profile.ActivitySource;
                activity.DialerProfileId = profile.ProfileId;
                activity.InteractionType = ActivityInteractionType.Manual;
                activity.AISessionId = null;

                // The dialer offers each record to whichever agent signed in to the campaign is free, so an assignee
                // routes nothing. Keeping one left the record in that user's own list as well, where it could be
                // called by hand while the dialer offered it to someone else.
                ResetAssignment(activity);

                await _omnichannelActivityManager.UpdateAsync(activity);

                // The change used to stop at the label: a record loaded by hand was never queued for the dialer, and a
                // queued one kept its old profile, so neither was dialed under the new one. It is queued on its
                // campaign now, or keeps its place there under the new profile.
                await dialerContributor.EnqueueAsync(activity.ItemId, activity.CampaignId, profile, HttpContext.RequestAborted);

                processedCount++;
            }
        }

        return processedCount;
    }

    private static bool IsBulkManageableActivity(OmnichannelActivity activity)
    {
        return activity is not null &&
            activity.Status is ActivityStatus.NotStated or ActivityStatus.Scheduled or ActivityStatus.Pending or ActivityStatus.AwaitingAgentResponse or ActivityStatus.Failed or ActivityStatus.Cancelled;
    }

    private static bool IsContactScheduledActivity(OmnichannelActivity activity, string contactContentItemId)
    {
        return activity is not null &&
            activity.Status == ActivityStatus.NotStated &&
            activity.InteractionType == ActivityInteractionType.Manual &&
            string.Equals(activity.ContactContentItemId, contactContentItemId, StringComparison.Ordinal);
    }

    private static void ResetAssignment(OmnichannelActivity activity)
    {
        activity.AssignedToId = null;
        activity.AssignedToUsername = null;
        activity.AssignedToUtc = null;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        ApplyInitialStatus(activity, hasAssignedUser: false);
        ClearReservationState(activity);
    }

    private static void ApplyInitialStatus(OmnichannelActivity activity, bool hasAssignedUser)
    {
        activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(activity.InteractionType, hasAssignedUser);

        // Re-arming an existing activity to a due status starts a fresh automated-processing budget so a previously
        // exhausted activity is not immediately re-failed by the background processor on its first transient error.
        activity.ProcessingAttempts = 0;
    }

    private static void ClearReservationState(OmnichannelActivity activity)
    {
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
    }

    private static string ResolveSourceForChangedSubject(string currentSource, ActivityInteractionType interactionType)
    {
        if (interactionType == ActivityInteractionType.Automated)
        {
            return ActivitySources.Automatic;
        }

        return string.Equals(currentSource, ActivitySources.Automatic, StringComparison.Ordinal)
            ? ActivitySources.Manual
            : currentSource;
    }

    private async Task<string> GetCurrentUserDisplayNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return S["Unknown user"].Value;
        }

        var displayName = await _displayNameProvider.GetAsync(user, HttpContext.RequestAborted);

        return string.IsNullOrWhiteSpace(displayName) ? S["Unknown user"].Value : displayName;
    }
}
