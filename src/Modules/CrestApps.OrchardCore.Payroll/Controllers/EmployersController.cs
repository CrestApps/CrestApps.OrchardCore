using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Authorization;
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
/// Payroll &gt; Management &gt; Employers, with Multiple Employers on. A person who manages payroll settings for every
/// employer adds and deletes them; one who manages them for some employers sees and changes only those.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.MultipleEmployers)]
public sealed class EmployersController : Controller
{
    private readonly IEmployerManager _manager;
    private readonly EmployerStore _store;
    private readonly IPayrollScopeService _scope;
    private readonly IEnumerable<IEmployerUsage> _usages;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<Employer> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public EmployersController(
        IEmployerManager manager,
        EmployerStore store,
        IPayrollScopeService scope,
        IEnumerable<IEmployerUsage> usages,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<Employer> displayManager,
        INotifier notifier,
        IHtmlLocalizer<EmployersController> htmlLocalizer,
        IStringLocalizer<EmployersController> stringLocalizer)
    {
        _manager = manager;
        _store = store;
        _scope = scope;
        _usages = usages;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/employers", "PayrollEmployersIndex")]
    public async Task<IActionResult> Index()
    {
        var scope = await _scope.GetScopeAsync(User, PayrollPermissions.ManagePayrollSettings, HttpContext.RequestAborted);

        if (scope.IsEmpty)
        {
            return Forbid();
        }

        var employers = (await _store.ListAsync(HttpContext.RequestAborted)).Where(employer => scope.Allows(employer.ItemId)).ToList();

        return View(new EmployersIndexViewModel
        {
            Employers = employers,
            CanAdd = scope.All,
            List = new AdminListContext<Employer> { Source = employers },
        });
    }

    [Admin("payroll/employers/create", "PayrollEmployersCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings))
        {
            return Forbid();
        }

        var employer = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New employer"],
            Editor = await _displayManager.BuildEditorAsync(employer, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/employers/create", "PayrollEmployersCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings))
        {
            return Forbid();
        }

        var employer = await _manager.NewAsync();
        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New employer"],
            Editor = await _displayManager.UpdateEditorAsync(employer, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, employer, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.CreateAsync(employer);
            await _notifier.SuccessAsync(H["The employer has been added."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("payroll/employers/edit/{id}", "PayrollEmployersEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        var employer = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        if (employer is null)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings, employer.ItemId))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = employer.Name,
            Editor = await _displayManager.BuildEditorAsync(employer, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/employers/edit/{id}", "PayrollEmployersEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        var employer = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        if (employer is null)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings, employer.ItemId))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = employer.Name,
            Editor = await _displayManager.UpdateEditorAsync(employer, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, employer, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _manager.UpdateAsync(employer);
            await _notifier.SuccessAsync(H["The employer has been updated."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("payroll/employers/delete/{id}", "PayrollEmployersDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings))
        {
            return Forbid();
        }

        var employer = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        if (employer is null)
        {
            return RedirectToAction(nameof(Index));
        }

        foreach (var usage in _usages)
        {
            if (await usage.GetUsagesAsync(employer, HttpContext.RequestAborted) is { Count: > 0 } uses)
            {
                await _notifier.ErrorAsync(H["It is in use and can't be removed: {0}. Make it inactive instead.", string.Join(", ", uses)]);

                return RedirectToAction(nameof(Index));
            }
        }

        if (await _manager.DeleteAsync(employer))
        {
            await _notifier.SuccessAsync(H["The employer has been removed."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
