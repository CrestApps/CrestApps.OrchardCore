using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Management &gt; Company: the site's own company as the employer that pays its staff, with its
/// registrations, address and the bank account it pays from. The first save adds it; it is the same editor the
/// employers list of Multiple Employers opens.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
public sealed class CompanyController : Controller
{
    private readonly IEmployerManager _manager;
    private readonly IPayrollCompanyService _company;
    private readonly IPayrollScopeService _scope;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<Employer> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CompanyController(
        IEmployerManager manager,
        IPayrollCompanyService company,
        IPayrollScopeService scope,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<Employer> displayManager,
        INotifier notifier,
        IHtmlLocalizer<CompanyController> htmlLocalizer,
        IStringLocalizer<CompanyController> stringLocalizer)
    {
        _manager = manager;
        _company = company;
        _scope = scope;
        _authorizationService = authorizationService;
        _siteService = siteService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/company", "PayrollCompany")]
    public async Task<IActionResult> Index()
    {
        var (company, isNew) = await FindAsync();

        if (!await MayManageAsync(company, isNew))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["Company"],
            Editor = await _displayManager.BuildEditorAsync(company, _updateModelAccessor.ModelUpdater, isNew),
        });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("payroll/company", "PayrollCompany")]
    public async Task<IActionResult> IndexPost()
    {
        var (company, isNew) = await FindAsync();

        if (!await MayManageAsync(company, isNew))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["Company"],
            Editor = await _displayManager.UpdateEditorAsync(company, _updateModelAccessor.ModelUpdater, isNew),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, company, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            if (isNew)
            {
                await _manager.CreateAsync(company);
                await _company.SetCompanyAsync(company.ItemId);
            }
            else
            {
                await _manager.UpdateAsync(company);
            }

            await _notifier.SuccessAsync(H["Your company's payroll details have been saved."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    // The company, or a new employer named after the site when it isn't set up yet.
    private async Task<(Employer Company, bool IsNew)> FindAsync()
    {
        if (await _company.GetCompanyAsync(HttpContext.RequestAborted) is { } company)
        {
            return (company, false);
        }

        var created = await _manager.NewAsync();

        if (string.IsNullOrEmpty(created.Name))
        {
            created.Name = (await _siteService.GetSiteSettingsAsync())?.SiteName;
        }

        return (created, true);
    }

    // Adding the company takes the global permission; changing it, the permission for it.
    private async Task<bool> MayManageAsync(Employer company, bool isNew)
        => isNew
            ? await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings)
            : await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings, company.ItemId);
}
