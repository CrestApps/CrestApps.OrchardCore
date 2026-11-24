using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The rows of the tax form list and the tax form editor: the form, its fields and their formulas, the yearly values,
/// the checks and how a recipient's statement is printed. A source adds a card of its own, such as Payroll's.
/// </summary>
internal sealed class TaxFormDisplayDriver : DisplayDriver<TaxForm>
{
    private readonly IEnumerable<ITaxFormUsage> _usages;
    private readonly TaxFormFunctionCatalog _functions;
    private readonly TaxFormSourceOptions _sources;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public TaxFormDisplayDriver(
        IEnumerable<ITaxFormUsage> usages,
        TaxFormFunctionCatalog functions,
        IOptions<TaxFormSourceOptions> sources,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<TaxFormDisplayDriver> stringLocalizer)
    {
        _usages = usages;
        _functions = functions;
        _sources = sources.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TaxForm form, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{form.Name} {form.DisplayName} {form.Jurisdiction} {form.Source}".ToLowerInvariant();
        }

        return CombineAsync(
            View("TaxForm_Fields_SummaryAdmin", form)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TaxForm_Tags_SummaryAdmin", form)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("TaxForm_Buttons_SummaryAdmin", form).Location("Actions:5"),
            View("TaxForm_ActionsMenu_SummaryAdmin", form).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(TaxForm form, BuildEditorContext context)
        => Editor(form, null, context.IsNew);

    public override async Task<IDisplayResult> UpdateAsync(TaxForm form, UpdateEditorContext context)
    {
        var model = new TaxFormViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // What uses a form (filings) keeps its name, so a used form keeps it (the page shows it read-only).
        if (context.IsNew || string.IsNullOrEmpty(form.Name) || !await IsUsedAsync(form))
        {
            form.Name = model.Name?.Trim().ToUpperInvariant();
        }

        form.DisplayName = model.DisplayName;
        form.Description = model.Description;
        form.Source = model.Source;
        form.Jurisdiction = model.Jurisdiction;
        form.Period = Enum.TryParse<TaxFormPeriod>(model.Period, out var period) ? period : TaxFormPeriod.Year;
        form.HasRecipientStatements = model.HasRecipientStatements;
        form.StatementPerJurisdiction = model.StatementPerJurisdiction;
        form.RecipientCondition = model.RecipientCondition;
        form.MissingTaxIdNote = model.MissingTaxIdNote;
        form.Enabled = model.Enabled;
        form.Statement = new TaxFormStatement
        {
            Name = Trim(model.StatementName),
            Title = Trim(model.StatementTitle),
            Copy = Trim(model.StatementCopy),
            TaxIdLabel = Trim(model.TaxIdLabel),
            TaxIdFormat = Trim(model.TaxIdFormat),
            FilerTaxIdLabel = Trim(model.FilerTaxIdLabel),
            FilerAccountJurisdiction = Trim(model.FilerAccountJurisdiction)?.ToUpperInvariant(),
            FileName = Trim(model.FileName),
        };

        // A row left empty is no field; the rows keep the order they were given in.
        form.Fields = [.. (model.Fields ?? [])
            .Where(field => !string.IsNullOrWhiteSpace(field.Number) || !string.IsNullOrWhiteSpace(field.Formula) || !string.IsNullOrWhiteSpace(field.Label))
            .Select(field => new TaxFormField
            {
                Number = field.Number?.Trim(),
                Label = Trim(field.Label),
                Level = Enum.TryParse<TaxFormFieldLevel>(field.Level, out var level) ? level : TaxFormFieldLevel.Recipient,
                Formula = Trim(field.Formula),
                AddToFiling = field.AddToFiling,
                AcceptsReportedAmounts = field.AcceptsReportedAmounts,
                Print = Enum.TryParse<TaxFormFieldPrint>(field.Print, out var print) ? print : TaxFormFieldPrint.Never,
            })];

        // The handler refuses a value without a name or a year.
        form.Values = [.. (model.Values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value.Name) || value.Amount is not null || value.Year is not null)
            .Select(value => new TaxFormValue { Name = value.Name?.Trim(), Year = value.Year ?? 0, Amount = value.Amount ?? 0 })];

        form.Checks = [.. (model.Checks ?? [])
            .Where(check => !string.IsNullOrWhiteSpace(check.Condition) || !string.IsNullOrWhiteSpace(check.Note))
            .Select(check => new TaxFormCheck { Condition = Trim(check.Condition), Note = Trim(check.Note) })];

        return Editor(form, model, context.IsNew);
    }

    private ShapeResult Editor(TaxForm form, TaxFormViewModel typed, bool isNew)
        => Initialize<TaxFormViewModel>("TaxFormFields_Edit", async model =>
        {
            var statement = form.Statement ?? new TaxFormStatement();
            var sourceName = string.IsNullOrEmpty(form.Source) ? _sources.Sources.FirstOrDefault()?.Name : form.Source;
            var source = _sources.Sources.FirstOrDefault(candidate => string.Equals(candidate.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            model.Name = typed?.Name ?? form.Name;
            model.DisplayName = typed?.DisplayName ?? form.DisplayName;
            model.Description = typed?.Description ?? form.Description;
            model.Source = sourceName;
            model.Jurisdiction = typed?.Jurisdiction ?? form.Jurisdiction;
            model.Period = form.Period.ToString();
            model.HasRecipientStatements = form.HasRecipientStatements;
            model.StatementPerJurisdiction = form.StatementPerJurisdiction;
            model.RecipientCondition = typed?.RecipientCondition ?? form.RecipientCondition;
            model.MissingTaxIdNote = typed?.MissingTaxIdNote ?? form.MissingTaxIdNote;
            model.Enabled = form.Enabled;
            model.StatementName = statement.Name;
            model.StatementTitle = statement.Title;
            model.StatementCopy = statement.Copy;
            model.TaxIdLabel = statement.TaxIdLabel;
            model.TaxIdFormat = statement.TaxIdFormat;
            model.FilerTaxIdLabel = statement.FilerTaxIdLabel;
            model.FilerAccountJurisdiction = statement.FilerAccountJurisdiction;
            model.FileName = statement.FileName;
            model.Fields = typed?.Fields ?? [.. form.Fields.Select(field => new TaxFormFieldViewModel
            {
                Number = field.Number,
                Label = field.Label,
                Level = field.Level.ToString(),
                Formula = field.Formula,
                AddToFiling = field.AddToFiling,
                AcceptsReportedAmounts = field.AcceptsReportedAmounts,
                Print = field.Print.ToString(),
            })];
            model.Values = typed?.Values ?? [.. form.Values
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Year)
                .Select(value => new TaxFormValueViewModel { Name = value.Name, Year = value.Year, Amount = value.Amount })];
            model.Checks = typed?.Checks ?? [.. form.Checks.Select(check => new TaxFormCheckViewModel { Condition = check.Condition, Note = check.Note })];
            model.IsUsed = !isNew && !string.IsNullOrEmpty(form.Name) && await IsUsedAsync(form);
            model.IsNew = isNew;
            model.HasRecipients = source?.HasRecipients ?? true;
            model.HasPeriod = source?.HasPeriod ?? true;
            model.RequiresJurisdiction = source?.RequiresJurisdiction ?? true;
            model.Sources = [.. _sources.Sources.Select(candidate => new SelectListItem(_localizerFactory.Localize(candidate.DisplayName).Value, candidate.Name, string.Equals(candidate.Name, sourceName, StringComparison.OrdinalIgnoreCase)))];
            model.ReportedAmountsLabel = source?.ReportedAmountsLabel is { } label ? _localizerFactory.Localize(label).Value : null;
            model.Functions = [.. _functions.For(sourceName).Functions
                .Where(function => !function.IsAggregate)

                // A source without recipients has nothing for the functions that read them.
                .Where(function => model.HasRecipients || function.Name is not ("RECIPIENTS" or "TOTAL"))
                .OrderBy(function => function.Category == TaxFormFunctions.Category ? 1 : IsBuiltIn(function.Category) ? 2 : 0)
                .ThenBy(function => function.Category, StringComparer.Ordinal)
                .ThenBy(function => function.Name, StringComparer.Ordinal)
                .Select(function => (function.Category, function.Signature, function.Description))];
        }).Location("Content:1");

    private async Task<bool> IsUsedAsync(TaxForm form)
    {
        foreach (var usage in _usages)
        {
            if ((await usage.GetUsagesAsync(form)).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBuiltIn(string category)
        => category is ExpressionCategories.Text or ExpressionCategories.Number or ExpressionCategories.Date or ExpressionCategories.Logical or ExpressionCategories.Aggregate;

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static class ExpressionCategories
    {
        public const string Text = global::OrchardCore.DataSources.Expressions.ExpressionFunctions.TextCategory;
        public const string Number = global::OrchardCore.DataSources.Expressions.ExpressionFunctions.NumberCategory;
        public const string Date = global::OrchardCore.DataSources.Expressions.ExpressionFunctions.DateCategory;
        public const string Logical = global::OrchardCore.DataSources.Expressions.ExpressionFunctions.LogicalCategory;
        public const string Aggregate = global::OrchardCore.DataSources.Expressions.ExpressionFunctions.AggregateCategory;
    }
}
