using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
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
/// Taxation › Tax Forms: the forms filers file, their fields and how each field is worked out.
/// </summary>
[Admin]
[Feature(TaxationConstants.Feature.TaxForms)]
public sealed class TaxFormsController : CatalogAdminController<TaxForm>
{
    private readonly IEnumerable<ITaxFormUsage> _usages;
    private readonly IAuthorizationService _authorizationService;
    private readonly TaxFormFunctionCatalog _functions;
    private readonly TaxFormSourceOptions _sources;

    public TaxFormsController(
        ITaxFormManager manager,
        IEnumerable<ITaxFormUsage> usages,
        TaxFormFunctionCatalog functions,
        IOptions<TaxFormSourceOptions> sources,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TaxForm> displayManager,
        INotifier notifier,
        IHtmlLocalizer<TaxFormsController> htmlLocalizer,
        IStringLocalizer<TaxFormsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _usages = usages;
        _functions = functions;
        _sources = sources.Value;
        _authorizationService = authorizationService;
    }

    protected override Permission ManagePermission
        => TaxationPermissions.ManageTaxForms;

    protected override LocalizedString CreateDisplayName
        => S["Tax form"];

    protected override LocalizedString NewDisplayName
        => S["New tax form"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The tax form has been added. Filers registered in its jurisdiction can prepare it where its source files forms, such as Payroll › Filings."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The tax form has been updated. Drafts are worked out again when they are prepared again; filed forms keep their amounts."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The tax form has been removed."];

    protected override string GetName(TaxForm entry)
        => string.IsNullOrWhiteSpace(entry?.DisplayName) ? entry?.Name ?? entry?.ItemId : entry.DisplayName;

    // A new form is of the source the page asks for (?source=SalesTax), else of the first one, so its editor shows that
    // source's card, functions and fields from the start.
    protected override Task<bool> InitializeNewAsync(TaxForm model)
    {
        var requested = Request.Query["source"].ToString();
        var source = _sources.Sources.FirstOrDefault(candidate => string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase))
            ?? _sources.Sources.FirstOrDefault();

        model.Source = source?.Name;

        return Task.FromResult(true);
    }

    // A used form (one with filings) is kept: what uses it names it. Turn it off instead.
    protected override async Task<bool> CanDeleteAsync(TaxForm model)
    {
        foreach (var usage in _usages)
        {
            if (await usage.GetUsagesAsync(model, HttpContext.RequestAborted) is { Count: > 0 } uses)
            {
                await Notifier.ErrorAsync(H["{0} is in use and can't be removed: {1}. Turn it off instead.", model.Name, string.Join(", ", uses)]);

                return false;
            }
        }

        return true;
    }

    [Admin("taxation/tax-forms", "TaxFormsIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("taxation/tax-forms/create", "TaxFormsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("taxation/tax-forms/create", "TaxFormsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("taxation/tax-forms/edit/{id}", "TaxFormsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("taxation/tax-forms/edit/{id}", "TaxFormsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Checks a field's formula on the editor, against the numbers of the form's other fields as the page has them and
    /// the functions of the form's source.
    /// </summary>
    /// <param name="request">The field and the numbers of the others.</param>
    /// <returns>Whether it works, and what is wrong.</returns>
    [HttpPost]
    [Admin("taxation/tax-forms/check-formula", "TaxFormsCheckFormula")]
    public async Task<IActionResult> CheckFormula(TaxFormFormulaCheck request)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxForms))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request?.Formula))
        {
            return Json(new { ok = true, message = S["No formula: the field is zero, plus what the source's records report to it."].Value });
        }

        var level = string.Equals(request.Level, nameof(TaxFormFieldLevel.Filing), StringComparison.Ordinal) ? TaxFormFieldLevel.Filing : TaxFormFieldLevel.Recipient;
        var form = new TaxForm { Name = "CHECK", DisplayName = "Check", Source = request.Source, Jurisdiction = "XX" };

        foreach (var number in (request.RecipientNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Distinct(StringComparer.Ordinal))
        {
            form.Fields.Add(new TaxFormField { Number = number.Trim(), Level = TaxFormFieldLevel.Recipient, AddToFiling = request.TotaledNumbers?.Contains(number) == true });
        }

        foreach (var number in (request.FilingNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Distinct(StringComparer.Ordinal))
        {
            form.Fields.Add(new TaxFormField { Number = number.Trim(), Level = TaxFormFieldLevel.Filing });
        }

        // The field itself, so a formula that reads it through others is caught too.
        var self = string.IsNullOrWhiteSpace(request.Number) ? "__field" : request.Number.Trim();

        form.Fields = [.. form.Fields.Where(field => !(field.Level == level && field.Number == self))];
        form.Fields.Add(new TaxFormField { Number = self, Level = level, Formula = request.Formula });

        return TaxFormFormulas.Compile(form, _functions.For(request.Source), S, out var errors) is null
            ? Json(new { ok = false, message = string.Join(" ", errors.Select(error => error.Message).Distinct(StringComparer.Ordinal)) })
            : Json(new { ok = true, message = S["The formula works."].Value });
    }

    [HttpPost]
    [Admin("taxation/tax-forms/delete/{id}", "TaxFormsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}

/// <summary>
/// A field's formula to check on the tax form editor, with the form's source and the numbers of its other fields.
/// </summary>
public sealed class TaxFormFormulaCheck
{
    public string Source { get; set; }

    public string Number { get; set; }

    public string Level { get; set; }

    public string Formula { get; set; }

    public string[] RecipientNumbers { get; set; } = [];

    public string[] TotaledNumbers { get; set; } = [];

    public string[] FilingNumbers { get; set; } = [];
}
