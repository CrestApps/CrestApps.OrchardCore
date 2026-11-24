using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
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
/// Payroll › Setup › Tax Forms: the forms employers file, their boxes and how each box is worked out.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
public sealed class PayrollTaxFormsController : CatalogAdminController<PayrollTaxForm>
{
    private readonly IPayrollFilingManager _filings;
    private readonly IAuthorizationService _authorizationService;
    private readonly PayrollTaxFormFunctionCatalog _functions;

    public PayrollTaxFormsController(
        IPayrollTaxFormManager manager,
        IPayrollFilingManager filings,
        PayrollTaxFormFunctionCatalog functions,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PayrollTaxForm> displayManager,
        INotifier notifier,
        IHtmlLocalizer<PayrollTaxFormsController> htmlLocalizer,
        IStringLocalizer<PayrollTaxFormsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _filings = filings;
        _functions = functions;
        _authorizationService = authorizationService;
    }

    protected override Permission ManagePermission
        => PayrollPermissions.ManagePayrollSettings;

    protected override LocalizedString CreateDisplayName
        => S["Tax form"];

    protected override LocalizedString NewDisplayName
        => S["New tax form"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The tax form has been added. Employers registered in its jurisdiction can prepare it under Payroll › Filings."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The tax form has been updated. Drafts are worked out again when they are prepared again; filed forms keep their amounts."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The tax form has been removed."];

    protected override string GetName(PayrollTaxForm entry)
        => string.IsNullOrWhiteSpace(entry?.DisplayName) ? entry?.Name ?? entry?.ItemId : entry.DisplayName;

    // A form with filings is kept: the filings name it. Turn it off instead.
    protected override async Task<bool> CanDeleteAsync(PayrollTaxForm model)
    {
        if ((await _filings.GetAllAsync(HttpContext.RequestAborted)).Any(filing => string.Equals(filing.Form, model.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await Notifier.ErrorAsync(H["Filings were prepared from {0}, so it can't be removed. Turn it off instead.", model.Name]);

            return false;
        }

        return true;
    }

    [Admin("payroll/setup/tax-forms", "PayrollTaxFormsIndex")]
    public Task<IActionResult> Index()
        => IndexAllAsync();

    [Admin("payroll/setup/tax-forms/create", "PayrollTaxFormsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/setup/tax-forms/create", "PayrollTaxFormsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("payroll/setup/tax-forms/edit/{id}", "PayrollTaxFormsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("payroll/setup/tax-forms/edit/{id}", "PayrollTaxFormsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Checks a field's formula on the editor, against the numbers of the form's other fields as the page has them.
    /// </summary>
    /// <param name="request">The field and the numbers of the others.</param>
    /// <returns>Whether it works, and what is wrong.</returns>
    [HttpPost]
    [Admin("payroll/setup/tax-forms/check-formula", "PayrollTaxFormsCheckFormula")]
    public async Task<IActionResult> CheckFormula(PayrollTaxFormFormulaCheck request)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManagePayrollSettings))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request?.Formula))
        {
            return Json(new { ok = true, message = S["No formula: the field is zero, plus what pay codes report to it."].Value });
        }

        var level = string.Equals(request.Level, nameof(PayrollTaxFormFieldLevel.Filing), StringComparison.Ordinal) ? PayrollTaxFormFieldLevel.Filing : PayrollTaxFormFieldLevel.Person;
        var form = new PayrollTaxForm { Name = "CHECK", DisplayName = "Check", Jurisdiction = "XX" };

        foreach (var number in (request.PersonNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Distinct(StringComparer.Ordinal))
        {
            form.Fields.Add(new PayrollTaxFormField { Number = number.Trim(), Level = PayrollTaxFormFieldLevel.Person, AddToFiling = request.TotaledNumbers?.Contains(number) == true });
        }

        foreach (var number in (request.FilingNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Distinct(StringComparer.Ordinal))
        {
            form.Fields.Add(new PayrollTaxFormField { Number = number.Trim(), Level = PayrollTaxFormFieldLevel.Filing });
        }

        // The field itself, so a formula that reads it through others is caught too.
        var self = string.IsNullOrWhiteSpace(request.Number) ? "__field" : request.Number.Trim();

        form.Fields = [.. form.Fields.Where(field => !(field.Level == level && field.Number == self))];
        form.Fields.Add(new PayrollTaxFormField { Number = self, Level = level, Formula = request.Formula });

        return PayrollTaxFormFormulas.Compile(form, _functions.Functions, S, out var errors) is null
            ? Json(new { ok = false, message = string.Join(" ", errors.Select(error => error.Message).Distinct(StringComparer.Ordinal)) })
            : Json(new { ok = true, message = S["The formula works."].Value });
    }

    [HttpPost]
    [Admin("payroll/setup/tax-forms/delete/{id}", "PayrollTaxFormsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);
}

/// <summary>
/// A field's formula to check on the tax form editor, with the numbers of the form's other fields.
/// </summary>
public sealed class PayrollTaxFormFormulaCheck
{
    public string Number { get; set; }

    public string Level { get; set; }

    public string Formula { get; set; }

    public string[] PersonNumbers { get; set; } = [];

    public string[] TotaledNumbers { get; set; } = [];

    public string[] FilingNumbers { get; set; } = [];
}
