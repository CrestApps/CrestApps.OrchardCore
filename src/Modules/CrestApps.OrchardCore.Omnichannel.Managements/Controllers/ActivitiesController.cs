using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

[Admin]
public sealed class ActivitiesController : Controller
{
    private readonly ISession _session;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OmnichannelActivityContainer> _containerDisplayManager;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ActivitiesController(
        ISession session,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelActivityContainer> containerDisplayManager,
        IAuthorizationService authorizationService,
        IStringLocalizer<ActivitiesController> stringLocalizer)
    {
        _session = session;
        _updateModelAccessor = updateModelAccessor;
        _containerDisplayManager = containerDisplayManager;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    [Admin("omnichannel/my-activities")]
    public async Task<IActionResult> MyActivities(
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
                    .OrderByDescending(x => x.ScheduledAt);

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

        var subjectIds = activities.Select(x => x.SubjectContentType)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        var contentItems = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId.IsIn(contactsIds) && index.Latest || index.ContentItemId.IsIn(subjectIds) && index.Published)
            .ListAsync();

        var contacts = await _session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(contactsIds) && index.Latest)
            .ListAsync();

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds))
            .ListAsync();

        var containerSummaries = new List<IShape>();

        foreach (var activity in activities)
        {
            var contact = contentItems.FirstOrDefault(x => x.ContentItemId == activity.ContactContentItemId);

            var subject = contentItems.FirstOrDefault(x => x.ContentItemId == activity.SubjectContentType);

            var user = users.FirstOrDefault(x => x.UserId == activity.AssignedToId);

            var container = new OmnichannelActivityContainer(activity, contact, user, subject);

            containerSummaries.Add(await _containerDisplayManager.BuildDisplayAsync(container, _updateModelAccessor.ModelUpdater, "SummaryAdmin"));
        }

        var model = new ListOmnichannelActivityContainer()
        {
            Containers = containerSummaries,
            Pager = pagerShape,
        };

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

        var contentItems = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == activity.ContactContentItemId && index.Latest || index.ContentItemId == activity.SubjectContentType && index.Published).ListAsync();

        var user = await _session.Query<User, UserIndex>(index => index.UserId == activity.AssignedToId).FirstOrDefaultAsync();

        var contact = contentItems.FirstOrDefault(x => x.ContentItemId == activity.ContactContentItemId);

        var container = new OmnichannelActivityContainer(activity, contact, user, contentItems.FirstOrDefault(x => x.ContentItemId == activity.SubjectContentType));

        var model = await _containerDisplayManager.BuildEditorAsync(container, _updateModelAccessor.ModelUpdater, isNew: false, "Process");

        return View(model);
    }

    public async Task<IActionResult> LoadActivities([FromServices] IDisplayManager<OmnichannelActivityBatch> displayManager)
    {
        var model = new OmnichannelActivityBatch();

        var shape = await displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true);

        return View(shape);
    }
}
