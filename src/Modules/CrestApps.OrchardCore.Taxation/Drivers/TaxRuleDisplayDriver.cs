using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// Renders the fields shared by every tax rule regardless of its calculation method. Method specific
/// inputs are contributed by <see cref="TaxRuleMethodDisplayDriver"/> and by any driver a third-party
/// calculation method registers for its own source.
/// </summary>
internal sealed class TaxRuleDisplayDriver : DisplayDriver<TaxRule>
{
    private readonly INamedCatalog<TaxJurisdiction> _jurisdictionStore;
    private readonly INamedCatalog<TaxCategory> _categoryStore;
    private readonly INamedCatalog<TaxType> _typeStore;

    internal readonly IStringLocalizer S;

    public TaxRuleDisplayDriver(
        INamedCatalog<TaxJurisdiction> jurisdictionStore,
        INamedCatalog<TaxCategory> categoryStore,
        INamedCatalog<TaxType> typeStore,
        IStringLocalizer<TaxRuleDisplayDriver> stringLocalizer)
    {
        _jurisdictionStore = jurisdictionStore;
        _categoryStore = categoryStore;
        _typeStore = typeStore;
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
            model.IncludedInPrice = rule.IncludedInPrice;
            model.IsCompound = rule.IsCompound;
            model.AppliesToShipping = rule.AppliesToShipping;
            model.MinimumAmount = rule.MinimumAmount;
            model.MaximumAmount = rule.MaximumAmount;
            model.EffectiveFromUtc = rule.EffectiveFromUtc;
            model.EffectiveToUtc = rule.EffectiveToUtc;

            var taxTypes = await _typeStore.GetAllAsync();

            model.TaxTypes = taxTypes
                .OrderBy(type => type.Name)
                .Select(type => new SelectListItem(type.Name, type.Name))
                .ToList();

            if (!string.IsNullOrEmpty(rule.TaxType) &&
                !model.TaxTypes.Any(item => string.Equals(item.Value, rule.TaxType, StringComparison.OrdinalIgnoreCase)))
            {
                model.TaxTypes.Insert(0, new SelectListItem(rule.TaxType, rule.TaxType));
            }

            model.CustomerTypes =
            [
                new SelectListItem(S["Any customer"], string.Empty),
                new SelectListItem(S["Consumer (B2C)"], nameof(CustomerTaxType.B2C)),
                new SelectListItem(S["Business (B2B)"], nameof(CustomerTaxType.B2B)),
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
        rule.TaxName = string.IsNullOrWhiteSpace(model.TaxName) ? null : model.TaxName.Trim();
        rule.TaxCode = model.TaxCode?.Trim();
        rule.JurisdictionId = string.IsNullOrEmpty(model.JurisdictionId) ? null : model.JurisdictionId;
        rule.CategoryCode = string.IsNullOrEmpty(model.CategoryCode) ? null : model.CategoryCode.Trim();
        rule.CustomerType = model.CustomerType;
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
