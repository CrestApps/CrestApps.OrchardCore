using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
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
/// The list of appointment types and their editor. The editor is built by the display drivers of
/// <see cref="AppointmentType"/>, so other modules add their own sections to it.
/// </summary>
public sealed class AppointmentTypesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AppointmentTypeStore _typeStore;
    private readonly IDisplayManager<AppointmentType> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTypesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="displayManager">The display manager that builds the appointment type editor.</param>
    /// <param name="updateModelAccessor">The accessor of the model updater the editor binds with.</param>
    /// <param name="session">The database session, cancelled when an edit is refused.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AppointmentTypesController(
        IAuthorizationService authorizationService,
        AppointmentTypeStore typeStore,
        IDisplayManager<AppointmentType> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<AppointmentTypesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _typeStore = typeStore;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the appointment types.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/types", "SchedulingAppointmentTypes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, types) = await _typeStore.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new SchedulingListViewModel
        {
            List = new AdminListContext<AppointmentType>
            {
                Source = types,
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing appointment type.
    /// </summary>
    /// <param name="id">The type's id, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("scheduling/types/edit/{id?}", "SchedulingAppointmentTypesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var type = isNew ? new AppointmentType() : await _typeStore.FindByIdAsync(id);

        if (type is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = type.Name,
            Editor = await _displayManager.BuildEditorAsync(type, _updateModelAccessor.ModelUpdater, isNew),
        });
    }

    /// <summary>
    /// Saves an appointment type.
    /// </summary>
    /// <param name="id">The type's id, or empty for a new one.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("scheduling/types/edit/{id?}", "SchedulingAppointmentTypesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var type = isNew ? new AppointmentType() : await _typeStore.FindByIdAsync(id);

        if (type is null)
        {
            return NotFound();
        }

        var displayName = type.Name;
        var editor = await _displayManager.UpdateEditorAsync(type, _updateModelAccessor.ModelUpdater, isNew);

        if (!ModelState.IsValid)
        {
            // A section that saved records of its own is rolled back with the type.
            await _session.CancelAsync();

            return View(new EditCatalogEntryViewModel
            {
                DisplayName = displayName,
                Editor = editor,
            });
        }

        if (isNew)
        {
            await _typeStore.CreateAsync(type);
        }
        else
        {
            await _typeStore.UpdateAsync(type);
        }

        await _notifier.SuccessAsync(H["The appointment type has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an appointment type.
    /// </summary>
    /// <param name="id">The type's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/types/delete/{id}", "SchedulingAppointmentTypesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var type = await _typeStore.FindByIdAsync(id);

        if (type is not null)
        {
            await _typeStore.DeleteAsync(type);
            await _notifier.SuccessAsync(H["The appointment type has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
