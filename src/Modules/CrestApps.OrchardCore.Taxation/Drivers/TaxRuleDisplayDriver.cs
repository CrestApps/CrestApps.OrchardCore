using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxRuleDisplayDriver : DisplayDriver<TaxRule>
{
    private static readonly string[] _taxTypes =
    [
        TaxTypeNames.SalesTax,
        TaxTypeNames.Vat,
        TaxTypeNames.Gst,
        TaxTypeNames.Hst,
        TaxTypeNames.Pst,
        TaxTypeNames.Qst,
        TaxTypeNames.ExciseTax,
        TaxTypeNames.AlcoholTax,
        TaxTypeNames.TobaccoTax,
        TaxTypeNames.TourismTax,
        TaxTypeNames.LodgingTax,
        TaxTypeNames.EnvironmentalTax,
        TaxTypeNames.DigitalServicesTax,
        TaxTypeNames.Other,
    ];

    private readonly INamedCatalog<TaxJurisdiction> _jurisdictionStore;
    private readonly INamedCatalog<TaxCategory> _categoryStore;
    private readonly INamedCatalog<TaxTable> _tableStore;
    private readonly IEnumerable<ITaxCalculationMethod> _calculationMethods;

    internal readonly IStringLocalizer S;

    public TaxRuleDisplayDriver(
        INamedCatalog<TaxJurisdiction> jurisdictionStore,
        INamedCatalog<TaxCategory> categoryStore,
        INamedCatalog<TaxTable> tableStore,
        IEnumerable<ITaxCalculationMethod> calculationMethods,
        IStringLocalizer<TaxRuleDisplayDriver> stringLocalizer)
    {
        _jurisdictionStore = jurisdictionStore;
        _categoryStore = categoryStore;
        _tableStore = tableStore;
        _calculationMethods = calculationMethods;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TaxRule rule, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TaxRule_Fields_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("TaxRule_Buttons_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("TaxRule_DefaultMeta_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(TaxRule rule, BuildEditorContext context)
    {
        return Initialize<TaxRuleViewModel>("TaxRuleFields_Edit", async model =>
        {
            model.IsNew = context.IsNew;
            model.Name = rule.Name;
            model.Enabled = rule.Enabled;
            model.Priority = rule.Priority;
            model.TaxType = rule.TaxType;
            model.TaxName = rule.TaxName;
            model.TaxCode = rule.TaxCode;
            model.JurisdictionId = rule.JurisdictionId;
            model.CategoryCode = rule.CategoryCode;
            model.CustomerType = rule.CustomerType;
            model.CalculationMethod = rule.CalculationMethod;
            model.Rate = rule.Rate;
            model.FixedAmount = rule.FixedAmount;
            model.TaxTableId = rule.TaxTableId;
            model.IncludedInPrice = rule.IncludedInPrice;
            model.IsCompound = rule.IsCompound;
            model.AppliesToShipping = rule.AppliesToShipping;
            model.MinimumAmount = rule.MinimumAmount;
            model.MaximumAmount = rule.MaximumAmount;
            model.EffectiveFromUtc = rule.EffectiveFromUtc;
            model.EffectiveToUtc = rule.EffectiveToUtc;

            model.TaxTypes = _taxTypes
                .Select(type => new SelectListItem(type, type))
                .ToList();

            model.CalculationMethods = _calculationMethods
                .Select(method => method.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .Select(name => new SelectListItem(name, name))
                .ToList();

            if (!string.IsNullOrEmpty(rule.CalculationMethod) &&
                !model.CalculationMethods.Any(item => string.Equals(item.Value, rule.CalculationMethod, StringComparison.OrdinalIgnoreCase)))
            {
                model.CalculationMethods.Insert(0, new SelectListItem(rule.CalculationMethod, rule.CalculationMethod));
            }

            model.MethodInputs = _calculationMethods
                .GroupBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Inputs, StringComparer.OrdinalIgnoreCase);

            model.CustomerTypes =
            [
                new SelectListItem(S["Any customer"], string.Empty),
                .. Enum.GetValues<CustomerTaxType>()
                    .Select(type => new SelectListItem(type.ToString(), type.ToString())),
            ];

            var jurisdictions = await _jurisdictionStore.GetAllAsync();

            model.Jurisdictions =
            [
                new SelectListItem(S["Any jurisdiction"], string.Empty),
                .. jurisdictions
                    .OrderBy(j => j.Name)
                    .Select(j => new SelectListItem(j.Name, j.ItemId)),
            ];

            var categories = await _categoryStore.GetAllAsync();

            model.Categories =
            [
                new SelectListItem(S["Any category"], string.Empty),
                .. categories
                    .Where(c => !string.IsNullOrEmpty(c.Code))
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem($"{c.Name} ({c.Code})", c.Code)),
            ];

            var tables = await _tableStore.GetAllAsync();

            model.TaxTables =
            [
                new SelectListItem(S["Select a tax table"], string.Empty),
                .. tables
                    .OrderBy(t => t.Name)
                    .Select(t => new SelectListItem(t.Name, t.ItemId)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxRule rule, UpdateEditorContext context)
    {
        var model = new TaxRuleViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            rule.Name = model.Name?.Trim();
        }

        rule.Enabled = model.Enabled;
        rule.Priority = model.Priority;
        rule.TaxType = model.TaxType?.Trim();
        rule.TaxName = model.TaxName?.Trim();
        rule.TaxCode = model.TaxCode?.Trim();
        rule.JurisdictionId = string.IsNullOrEmpty(model.JurisdictionId) ? null : model.JurisdictionId;
        rule.CategoryCode = string.IsNullOrEmpty(model.CategoryCode) ? null : model.CategoryCode.Trim();
        rule.CustomerType = model.CustomerType;
        rule.CalculationMethod = model.CalculationMethod?.Trim();

        var method = _calculationMethods
            .FirstOrDefault(m => string.Equals(m.Name, rule.CalculationMethod, StringComparison.OrdinalIgnoreCase));

        if (method is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CalculationMethod), S["The calculation method '{0}' is not registered. Enable the module that provides it before saving.", rule.CalculationMethod]);

            rule.Rate = model.Rate;
            rule.FixedAmount = model.FixedAmount;
            rule.TaxTableId = string.IsNullOrEmpty(model.TaxTableId) ? null : model.TaxTableId;
        }
        else
        {
            var inputs = method.Inputs;

            rule.Rate = inputs.HasFlag(TaxCalculationMethodInputs.Rate) ? model.Rate : null;
            rule.FixedAmount = inputs.HasFlag(TaxCalculationMethodInputs.FixedAmount) ? model.FixedAmount : null;

            if (inputs.HasFlag(TaxCalculationMethodInputs.TaxTable))
            {
                if (string.IsNullOrEmpty(model.TaxTableId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.TaxTableId), S["A tax table is required for the '{0}' calculation method.", rule.CalculationMethod]);
                }

                rule.TaxTableId = string.IsNullOrEmpty(model.TaxTableId) ? null : model.TaxTableId;
            }
            else
            {
                rule.TaxTableId = null;
            }
        }

        rule.IncludedInPrice = model.IncludedInPrice;
        rule.IsCompound = model.IsCompound;
        rule.AppliesToShipping = model.AppliesToShipping;
        rule.MinimumAmount = model.MinimumAmount;
        rule.MaximumAmount = model.MaximumAmount;
        rule.EffectiveFromUtc = model.EffectiveFromUtc;
        rule.EffectiveToUtc = model.EffectiveToUtc;

        return Edit(rule, context);
    }
}
