using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// The recurring earnings and deductions of a pay profile.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
// Pay is private: the signed-in person confirms their password before seeing it.
[ConfirmPassword]
public sealed class PayItemsController : Controller
{
    private readonly IPayItemManager _manager;
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _service;
    private readonly IPayCodeManager _codes;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollNames _names;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<EmployeePayItem> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PayItemsController(
        IPayItemManager manager,
        IPayProfileManager profiles,
        IPayProfileService service,
        IPayCodeManager codes,
        IPayrollScopeService scope,
        PayrollNames names,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<EmployeePayItem> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PayItemsController> htmlLocalizer,
        IStringLocalizer<PayItemsController> stringLocalizer)
    {
        _manager = manager;
        _profiles = profiles;
        _service = service;
        _codes = codes;
        _scope = scope;
        _names = names;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/employees/{profileId}/items", "PayrollPayItemsIndex")]
    public async Task<IActionResult> Index(string profileId)
    {
        if (await FindProfileAsync(profileId) is not { } profile)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayProfiles, profile.EmployerId))
        {
            return Forbid();
        }

        var items = await _manager.ListByProfileAsync(profile.ItemId, HttpContext.RequestAborted);

        return View(new PayItemsIndexViewModel
        {
            Profile = profile,
            StaffName = await _names.StaffAsync(profile.StaffMemberId),
            EmployerName = await _names.EmployerAsync(profile.EmployerId),
            Items = items,
            Codes = (await _codes.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(code => code.ItemId, StringComparer.Ordinal),
            CanManage = await MayManageAsync(profile),
            List = new AdminListContext<EmployeePayItem> { Source = items },
        });
    }

    [Admin("payroll/employees/{profileId}/items/create", "PayrollPayItemsCreate")]
    public async Task<IActionResult> Create(string profileId)
    {
        if (await FindProfileAsync(profileId) is not { } profile)
        {
            return NotFound();
        }

        if (!await MayManageAsync(profile))
        {
            return Forbid();
        }

        var item = await _manager.NewAsync();

        item.ProfileId = profile.ItemId;

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay item"],
            Editor = await _displayManager.BuildEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/employees/{profileId}/items/create", "PayrollPayItemsCreate")]
    public async Task<IActionResult> CreatePost(string profileId)
    {
        if (await FindProfileAsync(profileId) is not { } profile)
        {
            return NotFound();
        }

        if (!await MayManageAsync(profile))
        {
            return Forbid();
        }

        var item = await _manager.NewAsync();

        item.ProfileId = profile.ItemId;

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay item"],
            Editor = await _displayManager.UpdateEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, item, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.CreateAsync(item);
            await _notifier.SuccessAsync(H["The pay item has been added."]);

            return RedirectToAction(nameof(Index), new { profileId = profile.ItemId });
        }

        return View(model);
    }

    [Admin("payroll/employees/{profileId}/items/edit/{id}", "PayrollPayItemsEdit")]
    public async Task<IActionResult> Edit(string profileId, string id)
    {
        if (await FindAsync(profileId, id) is not { } found)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayProfiles, found.Profile.EmployerId))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = (await _codes.FindByIdAsync(found.Item.PayCodeId))?.Code ?? S["Pay item"],
            Editor = await _displayManager.BuildEditorAsync(found.Item, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/employees/{profileId}/items/edit/{id}", "PayrollPayItemsEdit")]
    public async Task<IActionResult> EditPost(string profileId, string id)
    {
        if (await FindAsync(profileId, id) is not { } found)
        {
            return NotFound();
        }

        if (!await MayManageAsync(found.Profile))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["Pay item"],
            Editor = await _displayManager.UpdateEditorAsync(found.Item, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, found.Item, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.UpdateAsync(found.Item);
            await _notifier.SuccessAsync(H["The pay item has been updated."]);

            return RedirectToAction(nameof(Index), new { profileId = found.Profile.ItemId });
        }

        return View(model);
    }

    [HttpPost]
    [Admin("payroll/employees/{profileId}/items/delete/{id}", "PayrollPayItemsDelete")]
    public async Task<IActionResult> Delete(string profileId, string id)
    {
        if (await FindAsync(profileId, id) is not { } found)
        {
            return RedirectToAction(nameof(Index), new { profileId });
        }

        if (!await MayManageAsync(found.Profile))
        {
            return Forbid();
        }

        if (await _manager.DeleteAsync(found.Item))
        {
            await _notifier.SuccessAsync(H["The pay item has been removed."]);
        }

        return RedirectToAction(nameof(Index), new { profileId });
    }

    private async Task<EmployeePayProfile> FindProfileAsync(string profileId)
        => string.IsNullOrEmpty(profileId) ? null : await _profiles.FindByIdAsync(profileId);

    private async Task<(EmployeePayProfile Profile, EmployeePayItem Item)?> FindAsync(string profileId, string id)
    {
        if (await FindProfileAsync(profileId) is not { } profile || string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } item || item.ProfileId != profile.ItemId)
        {
            return null;
        }

        return (profile, item);
    }

    private async Task<bool> MayManageAsync(EmployeePayProfile profile)
        => await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayProfiles, profile.EmployerId)
            && await _service.MayChangePayOfAsync(User, profile.StaffMemberId, HttpContext.RequestAborted);
}
