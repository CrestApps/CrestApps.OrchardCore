using CrestApps.OrchardCore.Core.Controllers;
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
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Setup &gt; Tax rules: the rules the built-in tax provider works taxes out with. They are the same for
/// every employer, so they need the permission for every employer.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.TaxTables)]
public sealed class PayrollTaxRulesController : CatalogAdminController<PayrollTaxRule>
{
    public PayrollTaxRulesController(
        IPayrollTaxRuleManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PayrollTaxRule> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PayrollTaxRulesController> htmlLocalizer,
        IStringLocalizer<PayrollTaxRulesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => PayrollPermissions.ManagePayrollSettings;

    protected override LocalizedString CreateDisplayName
        => S["Tax rule"];

    protected override LocalizedString NewDisplayName
        => S["New tax rule"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The tax rule has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The tax rule has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The tax rule has been removed."];

    [Admin("payroll/setup/tax-rules", "PayrollTaxRulesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("payroll/setup/tax-rules/create", "PayrollTaxRulesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/setup/tax-rules/create", "PayrollTaxRulesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("payroll/setup/tax-rules/edit/{id}", "PayrollTaxRulesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/setup/tax-rules/edit/{id}", "PayrollTaxRulesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("payroll/setup/tax-rules/delete/{id}", "PayrollTaxRulesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
