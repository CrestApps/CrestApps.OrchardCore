using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Commerce &gt; Taxation &gt; Income Tax Rules: the rules the built-in income tax provider works taxes on income out with. They are the
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

    /// <inheritdoc/>
    /// <remarks>
    /// A rule is one kind, chosen in "Add income tax rule" (<c>?source=FlatRate</c>) and fixed afterwards; its source's
    /// method is set from the start. With one source on offer, it is that one.
    /// </remarks>
    protected override Task<bool> InitializeNewAsync(IncomeTaxRule model)
    {
        var sources = HttpContext.RequestServices.GetRequiredService<IOptions<IncomeTaxRuleSourceOptions>>().Value.Sources;
        var requested = Request.Query["source"].ToString();

        if (string.IsNullOrWhiteSpace(requested) && sources.Count == 1)
        {
            requested = sources.Keys.First();
        }

        if (string.IsNullOrWhiteSpace(requested) || !sources.TryGetValue(requested, out var source))
        {
            return Task.FromResult(false);
        }

        model.Source = source.Name;
        model.Method = source.Method ?? TaxCalculationMethodNames.TaxTable;

        return Task.FromResult(true);
    }

    /// <summary>
    /// The view data key of the kinds of rule "Add income tax rule" offers, in order.
    /// </summary>
    public const string SourcesViewDataKey = "IncomeTaxRuleSources";

    [Admin("taxation/income-tax-rules", "IncomeTaxRulesIndex")]
    public Task<IActionResult> Index()
    {
        ViewData[SourcesViewDataKey] = ((IEnumerable<IncomeTaxRuleSource>)HttpContext.RequestServices.GetService<IOptions<IncomeTaxRuleSourceOptions>>()?.Value.Sources.Values ?? [])
            .OrderBy(source => source.Position)
            .ThenBy(source => source.Name, StringComparer.Ordinal)
            .ToList();

        return IndexAllAsync();
    }

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

    /// <summary>
    /// Marks a rule as checked against the agency's publications, by the signed-in user, now. Any later change to the
    /// rule clears the mark. It only informs: an unchecked rule is still worked out.
    /// </summary>
    /// <param name="id">The rule's id.</param>
    /// <param name="source">What the rule was checked against, such as "Michigan Form 446 (2026)", or none.</param>
    /// <returns>The list of rules.</returns>
    [HttpPost]
    [Admin("taxation/income-tax-rules/verify/{id}", "IncomeTaxRulesVerify")]
    public async Task<IActionResult> Verify(string id, string source = null)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, TaxationPermissions.VerifyIncomeTaxRules))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await Manager.FindByIdAsync(id) is not { } rule)
        {
            return NotFound();
        }

        var clock = HttpContext.RequestServices.GetRequiredService<IClock>();

        // The values are handed in, so the rule's handler keeps them rather than clearing the check as for an edit.
        await Manager.UpdateAsync(rule, new JsonObject
        {
            [nameof(IncomeTaxRule.VerifiedBy)] = User.Identity?.Name,
            [nameof(IncomeTaxRule.VerifiedUtc)] = clock.UtcNow,
            [nameof(IncomeTaxRule.VerificationSource)] = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
        });

        await Notifier.SuccessAsync(H["The income tax rule {0} is marked as verified.", rule.Name]);

        return RedirectToAction(nameof(Index));
    }
}
