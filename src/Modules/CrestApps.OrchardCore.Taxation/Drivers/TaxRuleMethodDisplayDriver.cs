using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// Renders the calculation-method specific fields of a tax rule. The fields shown are driven by the
/// <see cref="TaxCalculationMethodInputs"/> the rule's source declares, so a method only exposes the
/// inputs it actually consumes and no client-side toggling is required.
/// </summary>
internal sealed class TaxRuleMethodDisplayDriver : DisplayDriver<TaxRule>
{
    private readonly ITaxCalculationMethodProvider _methodProvider;
    private readonly INamedCatalog<TaxTable> _tableStore;

    internal readonly IStringLocalizer S;

    public TaxRuleMethodDisplayDriver(
        ITaxCalculationMethodProvider methodProvider,
        INamedCatalog<TaxTable> tableStore,
        IStringLocalizer<TaxRuleMethodDisplayDriver> stringLocalizer)
    {
        _methodProvider = methodProvider;
        _tableStore = tableStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TaxRule rule, BuildEditorContext context)
    {
        var method = _methodProvider.GetMethod(rule.Source);

        if (method is null || method.Inputs == TaxCalculationMethodInputs.None)
        {
            return null;
        }

        return Initialize<TaxRuleMethodViewModel>("TaxRuleMethodFields_Edit", async model =>
        {
            model.Rate = rule.Rate;
            model.FixedAmount = rule.FixedAmount;
            model.TaxTableId = rule.TaxTableId;
            model.ShowRate = method.Inputs.HasFlag(TaxCalculationMethodInputs.Rate);
            model.ShowFixedAmount = method.Inputs.HasFlag(TaxCalculationMethodInputs.FixedAmount);
            model.ShowTaxTable = method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable);

            if (model.ShowTaxTable)
            {
                var tables = await _tableStore.GetAllAsync();

                model.TaxTables =
                [
                    new SelectListItem(S["Select a tax table"], string.Empty),
                    .. tables
                        .OrderBy(t => t.Name)
                        .Select(t => new SelectListItem(t.Name, t.ItemId)),
                ];
            }
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxRule rule, UpdateEditorContext context)
    {
        var method = _methodProvider.GetMethod(rule.Source);

        if (method is null)
        {
            return Edit(rule, context);
        }

        var model = new TaxRuleMethodViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var inputs = method.Inputs;

        rule.Rate = inputs.HasFlag(TaxCalculationMethodInputs.Rate) ? model.Rate : null;
        rule.FixedAmount = inputs.HasFlag(TaxCalculationMethodInputs.FixedAmount) ? model.FixedAmount : null;

        if (inputs.HasFlag(TaxCalculationMethodInputs.TaxTable))
        {
            if (string.IsNullOrEmpty(model.TaxTableId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TaxTableId), S["A tax table is required for the '{0}' calculation method.", rule.Source]);
            }

            rule.TaxTableId = string.IsNullOrEmpty(model.TaxTableId) ? null : model.TaxTableId;
        }
        else
        {
            rule.TaxTableId = null;
        }

        return Edit(rule, context);
    }
}
