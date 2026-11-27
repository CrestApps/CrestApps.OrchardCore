using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Staff.Controllers;

/// <summary>
/// Staff &gt; Capabilities: the kinds of work people are put in pools by. Modules seed theirs; admins relabel, reorder or
/// turn them off, and add their own.
/// </summary>
public sealed class StaffCapabilitiesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStaffCapabilityService _capabilities;
    private readonly INotifier _notifier;
    private readonly IDisplayManager<StaffCapabilityEditViewModel> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;

    public StaffCapabilitiesController(
        IAuthorizationService authorizationService,
        IStaffCapabilityService capabilities,
        INotifier notifier,
        IDisplayManager<StaffCapabilityEditViewModel> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<StaffCapabilitiesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _capabilities = capabilities;
        _notifier = notifier;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        H = htmlLocalizer;
    }

    [Admin("staff/capabilities", "StaffCapabilities")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        return View(new StaffCapabilitiesListViewModel
        {
            List = new AdminListContext<StaffCapabilityInfo>
            {
                Source = [.. await _capabilities.ListAsync(includeDisabled: true, HttpContext.RequestAborted)],
            },
        });
    }

    [Admin("staff/capabilities/create", "StaffCapabilitiesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        var model = new StaffCapabilityEditViewModel { IsNew = true, Enabled = true, Position = 100 };

        model.Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true);

        return View("Edit", model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("staff/capabilities/create", "StaffCapabilitiesCreate")]
    public Task<IActionResult> CreatePost(StaffCapabilityEditViewModel model)
        => SaveAsync(model, isNew: true);

    [Admin("staff/capabilities/edit/{name}", "StaffCapabilitiesEdit")]
    public async Task<IActionResult> Edit(string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        var capability = await _capabilities.FindAsync(name, HttpContext.RequestAborted);

        if (capability is null)
        {
            return NotFound();
        }

        var model = new StaffCapabilityEditViewModel
        {
            Name = capability.Name,
            DisplayName = capability.DisplayName,
            Description = capability.Description,
            Position = capability.Position,
            Enabled = capability.Enabled,
            IsBuiltIn = capability.IsBuiltIn,
        };

        model.Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("staff/capabilities/edit/{name}", "StaffCapabilitiesEdit")]
    public Task<IActionResult> EditPost(string name, StaffCapabilityEditViewModel model)
    {
        // The key of a saved capability never changes: it is stored on staff members and modules rely on it.
        model.Name = name;

        return SaveAsync(model, isNew: false);
    }

    [HttpPost]
    [Admin("staff/capabilities/delete/{name}", "StaffCapabilitiesDelete")]
    public async Task<IActionResult> Delete(string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        var capability = await _capabilities.FindAsync(name, HttpContext.RequestAborted);

        if (capability is null)
        {
            return NotFound();
        }

        if (await _capabilities.DeleteAsync(name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(capability.IsBuiltIn
                ? H["{0} is back to its defaults.", capability.DisplayName]
                : H["{0} has been deleted. Staff members who had it keep it until they are saved again.", capability.DisplayName]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(StaffCapabilityEditViewModel model, bool isNew)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        var existing = isNew ? null : await _capabilities.FindAsync(model.Name, HttpContext.RequestAborted);

        if (!isNew && existing is null)
        {
            return NotFound();
        }

        model.IsNew = isNew;
        model.IsBuiltIn = existing?.IsBuiltIn ?? false;
        model.Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew);

        var errors = await _capabilities.SaveAsync(new StaffCapabilityDefinition
        {
            Name = model.Name,
            DisplayName = model.DisplayName,
            Description = model.Description,
            Position = model.Position,
            Enabled = model.Enabled,
        }, isNew, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Edit", model);
        }

        await _notifier.SuccessAsync(H["{0} has been saved.", model.DisplayName]);

        return RedirectToAction(nameof(Index));
    }
}
