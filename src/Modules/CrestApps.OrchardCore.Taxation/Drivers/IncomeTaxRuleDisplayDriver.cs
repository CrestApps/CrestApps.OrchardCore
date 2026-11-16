using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.Models;
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
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The rows and the editor of income tax rules.
/// </summary>
internal sealed class IncomeTaxRuleDisplayDriver : DisplayDriver<IncomeTaxRule>
{
    private readonly IncomeTaxCategoryOptions _categories;
    private readonly TaxCalculationMethodOptions _methods;
    private readonly INamedCatalog<TaxTable> _tables;
    private readonly IEnumerable<IIncomeTaxWithholdingMethod> _withholdingMethods;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public IncomeTaxRuleDisplayDriver(
        IOptions<IncomeTaxCategoryOptions> categories,
        IOptions<TaxCalculationMethodOptions> methods,
        INamedCatalog<TaxTable> tables,
        IEnumerable<IIncomeTaxWithholdingMethod> withholdingMethods,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<IncomeTaxRuleDisplayDriver> stringLocalizer)
    {
        _categories = categories.Value;
        _methods = methods.Value;
        _tables = tables;
        _withholdingMethods = withholdingMethods;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(IncomeTaxRule rule, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{rule.TaxCode} {rule.Name} {rule.Jurisdiction}".ToLowerInvariant();
        }

        return CombineAsync(
            View("IncomeTaxRule_Fields_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("IncomeTaxRule_Tags_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("IncomeTaxRule_Buttons_SummaryAdmin", rule).Location("Actions:5"),
            View("IncomeTaxRule_ActionsMenu_SummaryAdmin", rule).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxRuleViewModel>("IncomeTaxRuleFields_Edit", async model =>
        {
            model.Name = rule.Name;
            model.TaxCode = rule.TaxCode;
            model.Jurisdiction = rule.Jurisdiction;
            model.CollectedBy = rule.CollectedBy;
            model.ExceptJurisdictions = string.Join(", ", rule.ExceptJurisdictions ?? []);
            model.ByPlaceOfWork = rule.ByPlaceOfWork;
            model.Category = rule.Category;
            model.IncomeKinds = [.. (rule.IncomeKinds ?? []).Select(kind => kind.ToString())];
            model.Payer = rule.Payer.ToString();
            model.Method = rule.Method;
            model.Rate = Text(rule.Rate);
            model.FixedAmount = Text(rule.FixedAmount);
            model.TaxTableId = rule.TaxTableId;
            model.WageBase = Text(rule.WageBase);
            model.YearlyExemption = Text(rule.YearlyExemption);
            model.YearlyMaximum = Text(rule.YearlyMaximum);
            model.ShareOfTaxCode = rule.ShareOfTaxCode;
            model.ShareMultiplier = Text(rule.ShareMultiplier);
            model.Annualize = rule.Annualize;
            model.UsesElections = rule.UsesElections;
            model.AllowanceAmount = Text(rule.AllowanceAmount);
            model.SupplementalRate = Text(rule.SupplementalRate);
            model.SupplementalExcessRate = Text(rule.SupplementalExcessRate);
            model.SupplementalExcessThreshold = Text(rule.SupplementalExcessThreshold);
            model.WageThreshold = Text(rule.WageThreshold);

            // A method's tables and amounts post under "{method}:{key}"; only the chosen method's are kept.
            foreach (var (key, tableId) in rule.Tables ?? [])
            {
                model.WithholdingTables[$"{rule.Method}:{key}"] = tableId;
            }

            foreach (var (key, amount) in rule.Parameters ?? [])
            {
                model.WithholdingParameters[$"{rule.Method}:{key}"] = Text(amount);
            }

            model.EffectiveFrom = context.IsNew && rule.EffectiveFrom == default ? null : rule.EffectiveFrom;
            model.EffectiveTo = rule.EffectiveTo;
            model.Enabled = rule.Enabled;
            await OptionsAsync(model);
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var model = new IncomeTaxRuleViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        rule.Name = model.Name;
        rule.TaxCode = model.TaxCode;
        rule.Jurisdiction = model.Jurisdiction;
        rule.CollectedBy = string.IsNullOrWhiteSpace(model.CollectedBy) ? null : model.CollectedBy;
        rule.ExceptJurisdictions = [.. (model.ExceptJurisdictions ?? string.Empty).Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        rule.ByPlaceOfWork = model.ByPlaceOfWork;
        rule.Category = model.Category;
        rule.IncomeKinds = [.. (model.IncomeKinds ?? []).Select(kind => Enum.TryParse<IncomeKind>(kind, out var parsed) ? (IncomeKind?)parsed : null).Where(kind => kind is not null).Select(kind => kind.Value)];
        rule.Payer = Enum.TryParse<IncomeTaxPayer>(model.Payer, out var payer) ? payer : IncomeTaxPayer.Employee;
        rule.Method = model.Method;
        rule.Rate = Number(model.Rate, nameof(model.Rate), context);
        rule.FixedAmount = Number(model.FixedAmount, nameof(model.FixedAmount), context);
        rule.TaxTableId = string.IsNullOrEmpty(model.TaxTableId) ? null : model.TaxTableId;
        rule.WageBase = Number(model.WageBase, nameof(model.WageBase), context);
        rule.YearlyExemption = Number(model.YearlyExemption, nameof(model.YearlyExemption), context);
        rule.YearlyMaximum = Number(model.YearlyMaximum, nameof(model.YearlyMaximum), context);
        rule.ShareOfTaxCode = string.IsNullOrWhiteSpace(model.ShareOfTaxCode) ? null : model.ShareOfTaxCode;
        rule.ShareMultiplier = Number(model.ShareMultiplier, nameof(model.ShareMultiplier), context);
        rule.Annualize = model.Annualize;
        rule.UsesElections = model.UsesElections;
        rule.AllowanceAmount = Number(model.AllowanceAmount, nameof(model.AllowanceAmount), context);
        rule.SupplementalRate = Number(model.SupplementalRate, nameof(model.SupplementalRate), context);
        rule.SupplementalExcessRate = Number(model.SupplementalExcessRate, nameof(model.SupplementalExcessRate), context);
        rule.SupplementalExcessThreshold = Number(model.SupplementalExcessThreshold, nameof(model.SupplementalExcessThreshold), context);
        rule.WageThreshold = Number(model.WageThreshold, nameof(model.WageThreshold), context);
        rule.Tables = new Dictionary<string, string>(StringComparer.Ordinal);
        rule.Parameters = new Dictionary<string, decimal>(StringComparer.Ordinal);

        if (_withholdingMethods.FirstOrDefault(method => string.Equals(method.Name, rule.Method, StringComparison.OrdinalIgnoreCase)) is { } withholding)
        {
            foreach (var table in withholding.Tables)
            {
                if (model.WithholdingTables?.GetValueOrDefault($"{withholding.Name}:{table.Key}") is { Length: > 0 } tableId)
                {
                    rule.Tables[table.Key] = tableId;
                }
            }

            foreach (var parameter in withholding.Parameters)
            {
                if (Number(model.WithholdingParameters?.GetValueOrDefault($"{withholding.Name}:{parameter.Key}"), nameof(model.WithholdingParameters), context) is { } amount)
                {
                    rule.Parameters[parameter.Key] = amount;
                }
            }
        }

        rule.EffectiveTo = model.EffectiveTo;
        rule.Enabled = model.Enabled;

        // An empty first day is the handler's to refuse, as for a recipe.
        rule.EffectiveFrom = model.EffectiveFrom ?? default;

        return Edit(rule, context);
    }

    private async Task OptionsAsync(IncomeTaxRuleViewModel model)
    {
        model.AvailableIncomeKinds =
        [
            new SelectListItem(S["Employment income"], nameof(IncomeKind.Employment), model.IncomeKinds.Contains(nameof(IncomeKind.Employment))),
            new SelectListItem(S["Contractor payments"], nameof(IncomeKind.Contractor), model.IncomeKinds.Contains(nameof(IncomeKind.Contractor))),
            new SelectListItem(S["Pensions and annuities"], nameof(IncomeKind.Pension), model.IncomeKinds.Contains(nameof(IncomeKind.Pension))),
            new SelectListItem(S["Other income"], nameof(IncomeKind.Other), model.IncomeKinds.Contains(nameof(IncomeKind.Other))),
        ];
        model.Categories = [.. _categories.Categories.Select(category => new SelectListItem(_localizerFactory.Localize(category.DisplayName).Value, category.Name, category.Name == model.Category))];

        if (!string.IsNullOrEmpty(model.Category) && model.Categories.All(option => option.Value != model.Category))
        {
            model.Categories.Add(new SelectListItem(model.Category, model.Category, true));
        }

        model.Methods = [.. _methods.Methods.Values
            .Where(method => method.Name is not ("PerUnit" or "PerWeight" or "PerVolume"))
            .Select(method => new SelectListItem(method.DisplayName?.Value ?? method.Name, method.Name, string.Equals(method.Name, model.Method, StringComparison.OrdinalIgnoreCase)))];

        foreach (var withholding in _withholdingMethods)
        {
            var displayName = _localizerFactory.Localize(withholding.DisplayName).Value;

            model.Methods.Add(new SelectListItem(displayName, withholding.Name, string.Equals(withholding.Name, model.Method, StringComparison.OrdinalIgnoreCase)));
            model.WithholdingMethods.Add(new IncomeTaxWithholdingMethodViewModel
            {
                Name = withholding.Name,
                DisplayName = displayName,
                Tables = [.. withholding.Tables.Select(table => (table.Key, _localizerFactory.Localize(table.Label).Value))],
                Parameters = [.. withholding.Parameters.Select(parameter => (parameter.Key, _localizerFactory.Localize(parameter.Label).Value))],
            });
        }
        model.Tables = [.. (await _tables.GetAllAsync())
            .OrderBy(table => table.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(table => new SelectListItem(table.Name, table.ItemId, table.ItemId == model.TaxTableId))];
    }

    private decimal? Number(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Type a number of zero or more, with a dot for decimals."]);

        return null;
    }

    private static string Text(decimal? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture);
}
