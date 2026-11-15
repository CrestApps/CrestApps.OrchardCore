using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Garnishments: the orders of the employers a user may manage garnishments for. Nobody manages their own
/// orders, unless the setting allows changing one's own pay. An order that withheld something is released, never deleted.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Garnishments)]
public sealed class GarnishmentsController : Controller
{
    private readonly IGarnishmentOrderManager _manager;
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _service;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollNames _names;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<GarnishmentOrder> _displayManager;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public GarnishmentsController(
        IGarnishmentOrderManager manager,
        IPayProfileManager profiles,
        IPayProfileService service,
        IPayrollScopeService scope,
        PayrollNames names,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<GarnishmentOrder> displayManager,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<GarnishmentsController> htmlLocalizer,
        IStringLocalizer<GarnishmentsController> stringLocalizer)
    {
        _manager = manager;
        _profiles = profiles;
        _service = service;
        _scope = scope;
        _names = names;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _localClock = localClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/garnishments", "PayrollGarnishmentsIndex")]
    public async Task<IActionResult> Index()
    {
        var scope = await _scope.GetScopeAsync(User, PayrollPermissions.ManageGarnishments, HttpContext.RequestAborted);

        if (scope.IsEmpty)
        {
            return Forbid();
        }

        var orders = (await _manager.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds], HttpContext.RequestAborted))
            .OrderBy(order => order.Status)
            .ThenByDescending(order => order.ReceivedDate)
            .ToList();

        return View(new ListCatalogEntryViewModel<CatalogEntryViewModel<GarnishmentOrder>>
        {
            Models = [.. orders.Select(order => new CatalogEntryViewModel<GarnishmentOrder> { Model = order })],
            List = new AdminListContext<GarnishmentOrder> { Source = orders },
        });
    }

    [Admin("payroll/garnishments/create", "PayrollGarnishmentsCreate")]
    public async Task<IActionResult> Create(string profileId)
    {
        var scope = await _scope.GetScopeAsync(User, PayrollPermissions.ManageGarnishments, HttpContext.RequestAborted);

        if (scope.IsEmpty)
        {
            return Forbid();
        }

        var order = await _manager.NewAsync();

        if (!string.IsNullOrEmpty(profileId) && await _profiles.FindByIdAsync(profileId) is { } profile && await MayManageAsync(profile.EmployerId, profile.StaffMemberId))
        {
            order.ProfileId = profile.ItemId;
            order.EmployerId = profile.EmployerId;
            order.StaffMemberId = profile.StaffMemberId;
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New garnishment"],
            Editor = await _displayManager.BuildEditorAsync(order, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/garnishments/create", "PayrollGarnishmentsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if ((await _scope.GetScopeAsync(User, PayrollPermissions.ManageGarnishments, HttpContext.RequestAborted)).IsEmpty)
        {
            return Forbid();
        }

        var order = await _manager.NewAsync();
        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New garnishment"],
            Editor = await _displayManager.UpdateEditorAsync(order, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        // The profile chosen decides the employer: the user must manage that employer's garnishments, and not their own.
        var profile = string.IsNullOrEmpty(order.ProfileId) ? null : await _profiles.FindByIdAsync(order.ProfileId);

        if (profile is not null && !await MayManageAsync(profile.EmployerId, profile.StaffMemberId))
        {
            return Forbid();
        }

        if (await CatalogEntryValidation.ValidateAsync(_manager, order, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.CreateAsync(order);
            await _notifier.SuccessAsync(H["The garnishment has been added."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("payroll/garnishments/edit/{id}", "PayrollGarnishmentsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } order)
        {
            return NotFound();
        }

        if (!await MayManageAsync(order.EmployerId, order.StaffMemberId))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = await _names.StaffAsync(order.StaffMemberId) ?? S["Garnishment"],
            Editor = await _displayManager.BuildEditorAsync(order, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/garnishments/edit/{id}", "PayrollGarnishmentsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } order)
        {
            return NotFound();
        }

        if (!await MayManageAsync(order.EmployerId, order.StaffMemberId))
        {
            return Forbid();
        }

        var profileId = order.ProfileId;
        var model = new EditCatalogEntryViewModel
        {
            DisplayName = await _names.StaffAsync(order.StaffMemberId) ?? S["Garnishment"],
            Editor = await _displayManager.UpdateEditorAsync(order, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        // An order stays with its person: what it withheld is theirs.
        order.ProfileId = profileId;

        if (await CatalogEntryValidation.ValidateAsync(_manager, order, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.UpdateAsync(order);
            await _notifier.SuccessAsync(H["The garnishment has been updated."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("payroll/garnishments/delete/{id}", "PayrollGarnishmentsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } order)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await MayManageAsync(order.EmployerId, order.StaffMemberId))
        {
            return Forbid();
        }

        // Rule C: an order that withheld pay is part of the person's pay history.
        if (order.Remittances.Count > 0 || order.Withheld != 0)
        {
            await _notifier.ErrorAsync(H["The garnishment withheld pay, so it is kept: release it instead."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _manager.DeleteAsync(order))
        {
            await _notifier.SuccessAsync(H["The garnishment has been removed."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("payroll/garnishments/release/{id}", "PayrollGarnishmentsRelease")]
    public async Task<IActionResult> Release(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } order)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await MayManageAsync(order.EmployerId, order.StaffMemberId))
        {
            return Forbid();
        }

        order.Status = GarnishmentStatus.Released;
        order.EndDate ??= DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        // The order's rules run as on any save, so a release can't leave it in a state a recipe would refuse.
        if (!await CatalogEntryValidation.ValidateAsync(_manager, order, _updateModelAccessor.ModelUpdater, string.Empty))
        {
            foreach (var error in ModelState.Values.SelectMany(value => value.Errors))
            {
                await _notifier.ErrorAsync(H["{0}", error.ErrorMessage]);
            }

            return RedirectToAction(nameof(Index));
        }

        await _manager.UpdateAsync(order);
        await _notifier.SuccessAsync(H["The garnishment has been released: nothing more is withheld."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> MayManageAsync(string employerId, string staffMemberId)
        => !string.IsNullOrEmpty(employerId)
            && await _scope.AuthorizeAsync(User, PayrollPermissions.ManageGarnishments, employerId)
            && await _service.MayChangePayOfAsync(User, staffMemberId, HttpContext.RequestAborted);
}
