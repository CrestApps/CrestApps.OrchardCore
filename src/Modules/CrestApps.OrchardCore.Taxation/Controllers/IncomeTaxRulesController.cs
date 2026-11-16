using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
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

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Taxes &gt; Income tax rules: the rules the built-in income tax provider works taxes on income out with. They are the
/// same for every payer, and editing them is setup, so they need their own permission.
/// </summary>
[Admin]
[Feature(TaxationConstants.Feature.IncomeTax)]
public sealed class IncomeTaxRulesController : CatalogAdminController<IncomeTaxRule>
{
    public IncomeTaxRulesController(
        IIncomeTaxRuleManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<IncomeTaxRule> displayManager,
        INotifier notifier,
        IHtmlLocalizer<IncomeTaxRulesController> htmlLocalizer,
        IStringLocalizer<IncomeTaxRulesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission
        => TaxationPermissions.ManageIncomeTaxRules;

    protected override LocalizedString CreateDisplayName
        => S["Income tax rule"];

    protected override LocalizedString NewDisplayName
        => S["New income tax rule"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The income tax rule has been added."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The income tax rule has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The income tax rule has been removed."];

    [Admin("taxation/income-tax-rules", "IncomeTaxRulesIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("taxation/income-tax-rules/create", "IncomeTaxRulesCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("taxation/income-tax-rules/create", "IncomeTaxRulesCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("taxation/income-tax-rules/edit/{id}", "IncomeTaxRulesEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("taxation/income-tax-rules/edit/{id}", "IncomeTaxRulesEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("taxation/income-tax-rules/delete/{id}", "IncomeTaxRulesDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}
