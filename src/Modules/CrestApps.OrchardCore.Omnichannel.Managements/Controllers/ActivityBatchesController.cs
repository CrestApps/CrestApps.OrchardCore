using System.Security.Claims;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundJobs;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

[Admin]
public sealed class ActivityBatchesController : Controller
{
    private const int _batchSize = 100;

    private const string _optionsSearch = "Options.Search";

    private readonly ICatalogManager<OmnichannelActivityBatch> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OmnichannelActivityBatch> _batchDisplayDriver;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ActivityBatchesController(
        ICatalogManager<OmnichannelActivityBatch> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelActivityBatch> batchDisplayManager,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<ActivityBatchesController> htmlLocalizer,
        IStringLocalizer<ActivityBatchesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _batchDisplayDriver = batchDisplayManager;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<OmnichannelActivityBatch>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<OmnichannelActivityBatch>
            {
                Model = model,
                Shape = await _batchDisplayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("omnichannel/activity/batches/create", "OmnichannelActivityBatchesCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Activity Batch"],
            Editor = await _batchDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/activity/batches/create", "OmnichannelActivityBatchesCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Activity Batch"],
            Editor = await _batchDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);

            await _notifier.SuccessAsync(H["A new activity batch has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [Admin("omnichannel/activity/batches/edit/{id}", "OmnichannelActivityBatchesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _batchDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        ViewData["IsReadOnly"] = model.Status != OmnichannelActivityBatchStatus.New;

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/activity/batches/edit/{id}", "OmnichannelActivityBatchesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be edited."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch is being loaded and can't be edited."]);

            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _batchDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The Activity Batch has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        ViewData["IsReadOnly"] = model.Status != OmnichannelActivityBatchStatus.New;

        return View(viewModel);
    }

    [HttpPost]
    [Admin("omnichannel/activity/batches/delete/{id}", "OmnichannelActivityBatchesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch is being loaded and can't be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The activity batch has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the activity batch."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("omnichannel/activity/batches/load/{id}", "OmnichannelActivityBatchesLoad")]
    public async Task<ActionResult> Load(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be loaded again."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch was already being loaded and can't be loaded again."]);

            return RedirectToAction(nameof(Index));
        }

        model.Status = OmnichannelActivityBatchStatus.Started;
        await _manager.UpdateAsync(model);

        ShellScope.AddDeferredTask(s =>
        {
            // Query the contacts, and find the ones that do not already have an activity assigned.
            // Then, load the activities and assign them to the agents.
            return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("load-activity-batch", User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity.Name, model.ItemId, async (scope, loaderId, loaderUserName, batchId) =>
            {
                var catalog = scope.ServiceProvider.GetRequiredService<ICatalog<OmnichannelActivityBatch>>();

                long documentId = 0;
                var batch = await catalog.FindByIdAsync(batchId);

                if (batch.Status != OmnichannelActivityBatchStatus.Started)
                {
                    throw new InvalidOperationException($"Unable to load activities for batch with the ID '{batch.ItemId}' since it's status is not '{nameof(OmnichannelActivityBatchStatus.Started)}'.");
                }

                batch.Status = OmnichannelActivityBatchStatus.Loading;
                batch.TotalLoaded = 0;

                var batchCounter = 0;

                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ActivityBatchesController>>();
                var session = scope.ServiceProvider.GetRequiredService<ISession>();

                await using var readonlySession = session.Store.CreateSession(withTracking: false);

                var users = (await readonlySession.Query<User, UserIndex>(x => x.IsEnabled && x.UserId.IsIn(batch.UserIds)).ListAsync()).ToArray();

                if (users.Length == 0)
                {
                    batch.Status = OmnichannelActivityBatchStatus.New;

                    await catalog.UpdateAsync(batch);

                    logger.LogError("No valid users were found to assign the activities for the batch with ID '{BatchId}'.", batch.ItemId);
                    return;
                }

                var localClock = scope.ServiceProvider.GetRequiredService<ILocalClock>();
                var campaignCatalog = scope.ServiceProvider.GetRequiredService<ICatalog<OmnichannelCampaign>>();

                var campaign = await campaignCatalog.FindByIdAsync(batch.CampaignId);

                var activityCounter = 0;

                var activityManager = scope.ServiceProvider.GetRequiredService<IOmnichannelActivityManager>();

                while (true)
                {
                    var contactQuery = readonlySession.Query<ContentItem>();

                    if (batch.Channel == OmnichannelConstants.Channels.Sms)
                    {
                        contactQuery = contactQuery.With<OmnichannelContactIndex>(index => index.PrimaryCellPhoneNumber != null);
                    }
                    else if (batch.Channel == OmnichannelConstants.Channels.Phone)
                    {
                        contactQuery = contactQuery.With<OmnichannelContactIndex>(index => index.PrimaryCellPhoneNumber != null || index.PrimaryHomePhoneNumber != null);
                    }
                    else if (batch.Channel == OmnichannelConstants.Channels.Email)
                    {
                        contactQuery = contactQuery.With<OmnichannelContactIndex>(index => index.PrimaryEmailAddress != null);
                    }

                    contactQuery = contactQuery.With<ContentItemIndex>(index => index.ContentType == batch.ContactContentType && index.Published && index.DocumentId > documentId)
                        .OrderBy(x => x.DocumentId);

                    // Apply the filters logic
                    var contacts = await contactQuery
                        .Skip(batchCounter * _batchSize)
                        .Take(_batchSize)
                        .ListAsync();

                    if (!contacts.Any())
                    {
                        batch.Status = OmnichannelActivityBatchStatus.Loaded;

                        await catalog.UpdateAsync(batch);
                        break;
                    }
                    var preventDuplicates = true;

                    HashSet<string> inQueueActivities = null;

                    if (preventDuplicates)
                    {
                        var contentItemsIds = contacts.Select(x => x.ContentItemId).ToArray();

                        inQueueActivities = (await readonlySession.QueryIndex<OmnichannelActivityIndex>(index =>
                            index.ContactContentType == batch.ContactContentType &&
                            index.ContactContentItemId.IsIn(contentItemsIds) &&
                            index.Status != ActivityStatus.Completed &&
                            index.Status != ActivityStatus.Purged, collection: OmnichannelConstants.CollectionName)
                            .ListAsync())
                            .Select(x => x.ContactContentItemId)
                            .ToHashSet();
                    }

                    batchCounter++;
                    var now = _clock.UtcNow;

                    var scheduledUtc = await localClock.ConvertToUtcAsync(batch.ScheduleAt);

                    foreach (var contact in contacts)
                    {
                        if (preventDuplicates && inQueueActivities.Contains(contact.ContentItemId))
                        {
                            continue;
                        }

                        var user = users[activityCounter++ % users.Length];

                        documentId = Math.Min(documentId, contact.Id);

                        var activity = await activityManager.NewAsync();

                        activity.InteractionType = campaign.InteractionType;
                        activity.Channel = batch.Channel;
                        activity.ContactContentItemId = contact.ContentItemId;
                        activity.ContactContentType = batch.ContactContentType;
                        activity.SubjectContentType = batch.SubjectContentType;
                        activity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(contact, batch.Channel);
                        activity.ChannelEndpoint = campaign.ChannelEndpoint;
                        activity.AIProfileName = campaign.AIProfileName;
                        activity.CampaignId = batch.CampaignId;
                        activity.ScheduledUtc = scheduledUtc;
                        activity.AssignedToId = user.UserId;
                        activity.AssignedToUsername = user.UserName;
                        activity.AssignedToUtc = now;
                        activity.Instructions = batch.Instructions;
                        activity.CreatedUtc = now;
                        activity.CreatedById = loaderId;
                        activity.CreatedByUsername = loaderUserName;
                        activity.UrgencyLevel = batch.UrgencyLevel;
                        activity.Status = ActivityStatus.NotStated;

                        batch.TotalLoaded++;

                        await activityManager.CreateAsync(activity);
                        await session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName);
                    }

                    await catalog.UpdateAsync(batch);

                    // Flush the session to release memory.
                    await session.FlushAsync();
                }

                // Complete the batch loading.
                batch.Status = OmnichannelActivityBatchStatus.Loaded;
                await catalog.UpdateAsync(batch);
                await session.SaveChangesAsync();
            });
        });

        await _notifier.SuccessAsync(H["The Activity Batch started loaded in the background."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var instance = await _manager.FindByIdAsync(id);

                        if (instance == null)
                        {
                            continue;
                        }

                        if (await _manager.DeleteAsync(instance))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No activity batch were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 activity batch has been removed successfully.", "{0} activity batches have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
