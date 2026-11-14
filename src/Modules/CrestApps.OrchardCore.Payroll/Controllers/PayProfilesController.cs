using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Employees: the pay profiles of the employers the user may see. Changing one needs Manage pay profiles for
/// its employer, and nobody changes their own unless the settings allow it.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
public sealed class PayProfilesController : Controller
{
    private readonly IPayProfileManager _manager;
    private readonly IPayProfileService _service;
    private readonly DefaultPayProfileService _recorder;
    private readonly IPayrollScopeService _scope;
    private readonly EmployerStore _employers;
    private readonly PayrollNames _names;
    private readonly PayProfileEditTracker _tracker;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<EmployeePayProfile> _displayManager;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PayProfilesController(
        IPayProfileManager manager,
        IPayProfileService service,
        DefaultPayProfileService recorder,
        IPayrollScopeService scope,
        EmployerStore employers,
        PayrollNames names,
        PayProfileEditTracker tracker,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<EmployeePayProfile> displayManager,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<PayProfilesController> htmlLocalizer,
        IStringLocalizer<PayProfilesController> stringLocalizer)
    {
        _manager = manager;
        _service = service;
        _recorder = recorder;
        _scope = scope;
        _employers = employers;
        _names = names;
        _tracker = tracker;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/employees", "PayrollPayProfilesIndex")]
    public async Task<IActionResult> Index(string employerId)
    {
        var view = await _scope.GetScopeAsync(User, PayrollPermissions.ViewPayProfiles, HttpContext.RequestAborted);

        if (view.IsEmpty)
        {
            return Forbid();
        }

        var employers = (await _employers.ListAsync(HttpContext.RequestAborted)).Where(employer => view.Allows(employer.ItemId)).ToList();
        var chosen = employers.Any(employer => employer.ItemId == employerId) ? [employerId] : employers.Select(employer => employer.ItemId).ToArray();
        var profiles = (await _manager.ListByEmployersAsync(chosen, HttpContext.RequestAborted)).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var profile in profiles)
        {
            names[profile.ItemId] = await _names.StaffAsync(profile.StaffMemberId);
        }

        profiles = [.. profiles.OrderBy(profile => names[profile.ItemId], StringComparer.CurrentCultureIgnoreCase)];

        var model = new PayProfilesIndexViewModel
        {
            EmployerId = chosen.Length == 1 && employers.Count > 1 ? chosen[0] : null,
            Employers = [.. employers.Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == employerId))],
            CanAdd = !(await _scope.GetScopeAsync(User, PayrollPermissions.ManagePayProfiles, HttpContext.RequestAborted)).IsEmpty,
        };

        model.List = new AdminListContext<EmployeePayProfile>
        {
            Source = profiles,

            // The employer filter goes in the list's toolbar.
            ToolbarActions = await _shapeFactory.CreateAsync<PayProfilesIndexViewModel>("PayrollEmployerFilter", filter =>
            {
                filter.Employers = model.Employers;
            }),
        };

        return View(model);
    }

    [Admin("payroll/employees/create", "PayrollPayProfilesCreate")]
    public async Task<IActionResult> Create(string staffMemberId)
    {
        if ((await _scope.GetScopeAsync(User, PayrollPermissions.ManagePayProfiles, HttpContext.RequestAborted)).IsEmpty)
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(staffMemberId) && !await _service.MayChangePayOfAsync(User, staffMemberId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var profile = await _manager.NewAsync();

        profile.StaffMemberId = staffMemberId;

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay profile"],
            Editor = await _displayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/employees/create", "PayrollPayProfilesCreate")]
    public async Task<IActionResult> CreatePost(string staffMemberId)
    {
        if ((await _scope.GetScopeAsync(User, PayrollPermissions.ManagePayProfiles, HttpContext.RequestAborted)).IsEmpty)
        {
            return Forbid();
        }

        var profile = await _manager.NewAsync();

        profile.StaffMemberId = staffMemberId;

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay profile"],
            Editor = await _displayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        // The employer chosen must be one the user manages, and the person not the user, unless the settings allow it.
        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayProfiles, profile.EmployerId)
            || !await _service.MayChangePayOfAsync(User, profile.StaffMemberId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (await CatalogEntryValidation.ValidateAsync(_manager, profile, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.CreateAsync(profile);
            await TellAsync(profile);
            await _notifier.SuccessAsync(H["The pay profile has been added."]);

            return RedirectToAction(nameof(Edit), new { id = profile.ItemId });
        }

        return View(model);
    }

    [Admin("payroll/employees/edit/{id}", "PayrollPayProfilesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (await FindAsync(id) is not { } profile)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayProfiles, profile.EmployerId))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = await _names.StaffAsync(profile.StaffMemberId),
            Editor = await _displayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/employees/edit/{id}", "PayrollPayProfilesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (await FindAsync(id) is not { } profile)
        {
            return NotFound();
        }

        var employerBefore = profile.EmployerId;

        if (!await MayManageAsync(profile))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = await _names.StaffAsync(profile.StaffMemberId),
            Editor = await _displayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        // Moving the profile to another employer needs the same right there.
        if (profile.EmployerId != employerBefore && !await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayProfiles, profile.EmployerId))
        {
            return Forbid();
        }

        if (await CatalogEntryValidation.ValidateAsync(_manager, profile, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.UpdateAsync(profile);
            await TellAsync(profile);
            await _notifier.SuccessAsync(H["The pay profile has been updated."]);

            return RedirectToAction(nameof(Edit), new { id = profile.ItemId });
        }

        return View(model);
    }

    [HttpPost]
    [Admin("payroll/employees/delete/{id}", "PayrollPayProfilesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (await FindAsync(id) is not { } profile)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await MayManageAsync(profile))
        {
            return Forbid();
        }

        try
        {
            if (await _manager.DeleteAsync(profile))
            {
                await _notifier.SuccessAsync(H["The pay profile has been removed."]);
            }
        }
        catch (InvalidOperationException ex)
        {
            await _notifier.ErrorAsync(H["{0}", ex.Message]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the full tax id once, to a user who may see tax ids of the employer, and records who read it.
    /// </summary>
    /// <param name="id">The profile's id.</param>
    [HttpPost]
    [Admin("payroll/employees/tax-id/{id}", "PayrollPayProfilesTaxId")]
    public async Task<IActionResult> TaxId(string id)
    {
        if (await FindAsync(id) is not { } profile)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewTaxIds, profile.EmployerId))
        {
            return Forbid();
        }

        var taxId = await _service.RevealTaxIdAsync(profile, S["Shown on the pay profile page"], HttpContext.RequestAborted);

        if (taxId is null)
        {
            await _notifier.ErrorAsync(H["The tax id can't be read on this site, for example after the database was copied from another one. Enter it again."]);

            return RedirectToAction(nameof(Edit), new { id });
        }

        // The page holds a full tax id: browsers and proxies must not keep it.
        Response.Headers.CacheControl = "no-store";

        return View(new PayProfileTaxIdViewModel
        {
            ProfileId = profile.ItemId,
            StaffName = await _names.StaffAsync(profile.StaffMemberId),
            TaxId = taxId,
        });
    }

    private async Task<EmployeePayProfile> FindAsync(string id)
        => string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

    private async Task<bool> MayManageAsync(EmployeePayProfile profile)
        => await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayProfiles, profile.EmployerId)
            && await _service.MayChangePayOfAsync(User, profile.StaffMemberId, HttpContext.RequestAborted);

    private async Task TellAsync(EmployeePayProfile profile)
    {
        foreach (var kind in _tracker.Changes.Distinct())
        {
            await _recorder.TellAsync(profile, kind, S["Changed on the pay profile page"], HttpContext.RequestAborted);
        }

        await _recorder.TellAsync(profile, PayProfileChangeKind.Changed, S["Saved on the pay profile page"], HttpContext.RequestAborted);
    }
}
