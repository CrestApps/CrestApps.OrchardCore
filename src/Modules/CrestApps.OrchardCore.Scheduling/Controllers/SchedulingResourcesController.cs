using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The list of scheduling resources and their editor. The editor is built by the display drivers of
/// <see cref="SchedulingResource"/>, so other modules add their own sections to it.
/// </summary>
public sealed class SchedulingResourcesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly SchedulingResourceCalendars _resourceCalendars;
    private readonly IDisplayManager<SchedulingResource> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourcesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resourceCalendars">The resource calendar service.</param>
    /// <param name="displayManager">The display manager that builds the resource editor.</param>
    /// <param name="updateModelAccessor">The accessor of the model updater the editor binds with.</param>
    /// <param name="session">The database session, cancelled when an edit is refused.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public SchedulingResourcesController(
        IAuthorizationService authorizationService,
        SchedulingResourceStore resourceStore,
        SchedulingResourceCalendars resourceCalendars,
        IDisplayManager<SchedulingResource> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<SchedulingResourcesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _resourceStore = resourceStore;
        _resourceCalendars = resourceCalendars;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the resources.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/resources", "SchedulingResources")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, resources) = await _resourceStore.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new SchedulingListViewModel
        {
            List = new AdminListContext<SchedulingResource>
            {
                Source = resources,
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing resource.
    /// </summary>
    /// <param name="id">The resource's id, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("scheduling/resources/edit/{id?}", "SchedulingResourcesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var resource = isNew ? NewResource() : await _resourceStore.FindByIdAsync(id);

        if (resource is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = resource.Name,
            Editor = await _displayManager.BuildEditorAsync(resource, _updateModelAccessor.ModelUpdater, isNew),
        });
    }

    /// <summary>
    /// Saves a resource.
    /// </summary>
    /// <param name="id">The resource's id, or empty for a new one.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("scheduling/resources/edit/{id?}", "SchedulingResourcesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var resource = isNew ? NewResource() : await _resourceStore.FindByIdAsync(id);

        if (resource is null)
        {
            return NotFound();
        }

        var displayName = resource.Name;
        var editor = await _displayManager.UpdateEditorAsync(resource, _updateModelAccessor.ModelUpdater, isNew);

        if (!ModelState.IsValid)
        {
            // A section that saved records of its own is rolled back with the resource.
            await _session.CancelAsync();

            return View(new EditCatalogEntryViewModel
            {
                DisplayName = displayName,
                Editor = editor,
            });
        }

        if (isNew)
        {
            resource.ItemId = CrestApps.Core.UniqueId.GenerateId();
            await _resourceCalendars.EnsureCalendarAsync(resource);
            await _resourceStore.CreateAsync(resource);
        }
        else
        {
            await _resourceCalendars.EnsureCalendarAsync(resource);
            await _resourceStore.UpdateAsync(resource);
        }

        await _notifier.SuccessAsync(H["The resource has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="id">The resource's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/resources/delete/{id}", "SchedulingResourcesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var resource = await _resourceStore.FindByIdAsync(id);

        if (resource is not null)
        {
            await _resourceStore.DeleteAsync(resource);
            await _notifier.SuccessAsync(H["The resource has been deleted. Its calendar and appointments are kept."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private static SchedulingResource NewResource()
    {
        var resource = new SchedulingResource();

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "09:00", End = "17:00" });
        }

        return resource;
    }
}
