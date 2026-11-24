using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the tax form list and the tax form editor: who the form reports, its boxes and their formulas, the
/// yearly values, the checks and how a person's statement is printed.
/// </summary>
internal sealed class PayrollTaxFormDisplayDriver : DisplayDriver<PayrollTaxForm>
{
    private readonly IPayrollFilingManager _filings;
    private readonly PayrollTaxFormFunctionCatalog _functions;

    internal readonly IStringLocalizer S;

    public PayrollTaxFormDisplayDriver(
        IPayrollFilingManager filings,
        PayrollTaxFormFunctionCatalog functions,
        IStringLocalizer<PayrollTaxFormDisplayDriver> stringLocalizer)
    {
        _filings = filings;
        _functions = functions;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayrollTaxForm form, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{form.Name} {form.DisplayName} {form.Jurisdiction}".ToLowerInvariant();
        }

        return CombineAsync(
            View("PayrollTaxForm_Fields_SummaryAdmin", form)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayrollTaxForm_Tags_SummaryAdmin", form)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayrollTaxForm_Buttons_SummaryAdmin", form).Location("Actions:5"),
            View("PayrollTaxForm_ActionsMenu_SummaryAdmin", form).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(PayrollTaxForm form, BuildEditorContext context)
        => Editor(form, null, context.IsNew);

    public override async Task<IDisplayResult> UpdateAsync(PayrollTaxForm form, UpdateEditorContext context)
    {
        var model = new PayrollTaxFormViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Filings keep the form's name, so a form with filings keeps it (the page shows it read-only).
        if (context.IsNew || string.IsNullOrEmpty(form.Name) || !await HasFilingsAsync(form.Name))
        {
            form.Name = model.Name?.Trim().ToUpperInvariant();
        }

        form.DisplayName = model.DisplayName;
        form.Description = model.Description;
        form.Jurisdiction = model.Jurisdiction;
        form.Period = Enum.TryParse<PayrollTaxFormPeriod>(model.Period, out var period) ? period : PayrollTaxFormPeriod.Year;
        form.Workers = Enum.TryParse<PayrollTaxFormWorkers>(model.Workers, out var workers) ? workers : PayrollTaxFormWorkers.Employees;
        form.PeopleJurisdiction = model.PeopleJurisdiction;
        form.HasPersonStatements = model.HasPersonStatements;
        form.StatementPerJurisdiction = model.StatementPerJurisdiction;
        form.ListJurisdictionAmounts = model.ListJurisdictionAmounts;
        form.PersonCondition = model.PersonCondition;
        form.MissingTaxIdNote = model.MissingTaxIdNote;
        form.PayCodeFieldPrefix = model.PayCodeFieldPrefix;
        form.Enabled = model.Enabled;
        form.Statement = new PayrollTaxFormStatement
        {
            Name = Trim(model.StatementName),
            Title = Trim(model.StatementTitle),
            Copy = Trim(model.StatementCopy),
            TaxIdLabel = Trim(model.TaxIdLabel),
            TaxIdFormat = Trim(model.TaxIdFormat),
            EmployerTaxIdLabel = Trim(model.EmployerTaxIdLabel),
            EmployerAccountJurisdiction = Trim(model.EmployerAccountJurisdiction)?.ToUpperInvariant(),
            FileName = Trim(model.FileName),
        };

        // A row left empty is no field; the rows keep the order they were given in.
        form.Fields = [.. (model.Fields ?? [])
            .Where(field => !string.IsNullOrWhiteSpace(field.Number) || !string.IsNullOrWhiteSpace(field.Formula) || !string.IsNullOrWhiteSpace(field.Label))
            .Select(field => new PayrollTaxFormField
            {
                Number = field.Number?.Trim(),
                Label = Trim(field.Label),
                Level = Enum.TryParse<PayrollTaxFormFieldLevel>(field.Level, out var level) ? level : PayrollTaxFormFieldLevel.Person,
                Formula = Trim(field.Formula),
                AddToFiling = field.AddToFiling,
                PayCodes = field.PayCodes,
                Print = Enum.TryParse<PayrollTaxFormFieldPrint>(field.Print, out var print) ? print : PayrollTaxFormFieldPrint.Never,
            })];

        // The handler refuses a value without a name or a year.
        form.Values = [.. (model.Values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value.Name) || value.Amount is not null || value.Year is not null)
            .Select(value => new PayrollTaxFormValue { Name = value.Name?.Trim(), Year = value.Year ?? 0, Amount = value.Amount ?? 0 })];

        form.Checks = [.. (model.Checks ?? [])
            .Where(check => !string.IsNullOrWhiteSpace(check.Condition) || !string.IsNullOrWhiteSpace(check.Note))
            .Select(check => new PayrollTaxFormCheck { Condition = Trim(check.Condition), Note = Trim(check.Note) })];

        return Editor(form, model, context.IsNew);
    }

    private ShapeResult Editor(PayrollTaxForm form, PayrollTaxFormViewModel typed, bool isNew)
        => Initialize<PayrollTaxFormViewModel>("PayrollTaxFormFields_Edit", async model =>
        {
            var statement = form.Statement ?? new PayrollTaxFormStatement();

            model.Name = typed?.Name ?? form.Name;
            model.DisplayName = typed?.DisplayName ?? form.DisplayName;
            model.Description = typed?.Description ?? form.Description;
            model.Jurisdiction = typed?.Jurisdiction ?? form.Jurisdiction;
            model.Period = form.Period.ToString();
            model.Workers = form.Workers.ToString();
            model.PeopleJurisdiction = typed?.PeopleJurisdiction ?? form.PeopleJurisdiction;
            model.HasPersonStatements = form.HasPersonStatements;
            model.StatementPerJurisdiction = form.StatementPerJurisdiction;
            model.ListJurisdictionAmounts = form.ListJurisdictionAmounts;
            model.PersonCondition = typed?.PersonCondition ?? form.PersonCondition;
            model.MissingTaxIdNote = typed?.MissingTaxIdNote ?? form.MissingTaxIdNote;
            model.PayCodeFieldPrefix = typed?.PayCodeFieldPrefix ?? form.PayCodeFieldPrefix;
            model.Enabled = form.Enabled;
            model.StatementName = statement.Name;
            model.StatementTitle = statement.Title;
            model.StatementCopy = statement.Copy;
            model.TaxIdLabel = statement.TaxIdLabel;
            model.TaxIdFormat = statement.TaxIdFormat;
            model.EmployerTaxIdLabel = statement.EmployerTaxIdLabel;
            model.EmployerAccountJurisdiction = statement.EmployerAccountJurisdiction;
            model.FileName = statement.FileName;
            model.Fields = typed?.Fields ?? [.. form.Fields.Select(field => new PayrollTaxFormFieldViewModel
            {
                Number = field.Number,
                Label = field.Label,
                Level = field.Level.ToString(),
                Formula = field.Formula,
                AddToFiling = field.AddToFiling,
                PayCodes = field.PayCodes,
                Print = field.Print.ToString(),
            })];
            model.Values = typed?.Values ?? [.. form.Values
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Year)
                .Select(value => new PayrollTaxFormValueViewModel { Name = value.Name, Year = value.Year, Amount = value.Amount })];
            model.Checks = typed?.Checks ?? [.. form.Checks.Select(check => new PayrollTaxFormCheckViewModel { Condition = check.Condition, Note = check.Note })];
            model.IsFiled = !isNew && !string.IsNullOrEmpty(form.Name) && await HasFilingsAsync(form.Name);
            model.Functions = [.. _functions.Functions.Functions
                .Where(function => !function.IsAggregate)
                .OrderBy(function => function.Category == PayrollTaxFormFunctions.Category ? 0 : 1)
                .ThenBy(function => function.Category, StringComparer.Ordinal)
                .ThenBy(function => function.Name, StringComparer.Ordinal)
                .Select(function => (function.Category, function.Signature, function.Description))];
        }).Location("Content:1");

    private async Task<bool> HasFilingsAsync(string name)
        => (await _filings.GetAllAsync()).Any(filing => string.Equals(filing.Form, name, StringComparison.OrdinalIgnoreCase));

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
