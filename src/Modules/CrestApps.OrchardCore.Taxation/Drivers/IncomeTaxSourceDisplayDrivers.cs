using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// A flat rate rule's fields: the rate (or the payer's own) and the yearly wage base, threshold, exemption and maximum.
/// </summary>
internal sealed class IncomeTaxFlatRateDisplayDriver : IncomeTaxRuleSourceDisplayDriver
{
    public IncomeTaxFlatRateDisplayDriver(IStringLocalizer<IncomeTaxFlatRateDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    protected override string Source
        => IncomeTaxRuleSources.FlatRate;

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxFlatRateViewModel>("IncomeTaxFlatRate_Edit", model =>
        {
            model.Rate = Text(rule.Rate);
            model.RateFromRegistration = rule.RateFromRegistration;
            model.WageBase = Text(rule.WageBase);
            model.WageThreshold = Text(rule.WageThreshold);
            model.YearlyExemption = Text(rule.YearlyExemption);
            model.YearlyMaximum = Text(rule.YearlyMaximum);
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var model = new IncomeTaxFlatRateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ClearAmounts(rule);
        rule.Method = TaxCalculationMethodNames.Percentage;
        rule.Rate = Number(model.Rate, nameof(model.Rate), context);
        rule.RateFromRegistration = model.RateFromRegistration;
        rule.WageBase = Number(model.WageBase, nameof(model.WageBase), context);
        rule.WageThreshold = Number(model.WageThreshold, nameof(model.WageThreshold), context);
        rule.YearlyExemption = Number(model.YearlyExemption, nameof(model.YearlyExemption), context);
        rule.YearlyMaximum = Number(model.YearlyMaximum, nameof(model.YearlyMaximum), context);

        return Edit(rule, context);
    }
}

/// <summary>
/// A share of another tax's fields: which tax, how many times it, and the yearly maximum.
/// </summary>
internal sealed class IncomeTaxShareOfTaxDisplayDriver : IncomeTaxRuleSourceDisplayDriver
{
    public IncomeTaxShareOfTaxDisplayDriver(IStringLocalizer<IncomeTaxShareOfTaxDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    protected override string Source
        => IncomeTaxRuleSources.ShareOfTax;

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxShareOfTaxViewModel>("IncomeTaxShareOfTax_Edit", model =>
        {
            model.ShareOfTaxCode = rule.ShareOfTaxCode;
            model.ShareMultiplier = Text(rule.ShareMultiplier);
            model.YearlyMaximum = Text(rule.YearlyMaximum);
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var model = new IncomeTaxShareOfTaxViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ClearAmounts(rule);
        rule.Method = TaxCalculationMethodNames.Percentage;
        rule.ShareOfTaxCode = string.IsNullOrWhiteSpace(model.ShareOfTaxCode) ? null : model.ShareOfTaxCode;
        rule.ShareMultiplier = Number(model.ShareMultiplier, nameof(model.ShareMultiplier), context);
        rule.YearlyMaximum = Number(model.YearlyMaximum, nameof(model.YearlyMaximum), context);

        return Edit(rule, context);
    }
}

/// <summary>
/// A tax table rule's fields: the table method and table, the wage base, whether it works on a year's income with the
/// person's election, and how supplemental income is withheld.
/// </summary>
internal sealed class IncomeTaxTableDisplayDriver : IncomeTaxRuleSourceDisplayDriver
{
    private readonly TaxCalculationMethodOptions _methods;
    private readonly ITaxCalculationMethodProvider _methodProvider;
    private readonly INamedCatalog<TaxTable> _tables;

    public IncomeTaxTableDisplayDriver(
        IOptions<TaxCalculationMethodOptions> methods,
        ITaxCalculationMethodProvider methodProvider,
        INamedCatalog<TaxTable> tables,
        IStringLocalizer<IncomeTaxTableDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
        _methods = methods.Value;
        _methodProvider = methodProvider;
        _tables = tables;
    }

    protected override string Source
        => IncomeTaxRuleSources.TaxTable;

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxTableViewModel>("IncomeTaxTable_Edit", async model =>
        {
            model.Method = rule.Method;
            model.TaxTableId = rule.TaxTableId;
            model.WageBase = Text(rule.WageBase);
            model.Annualize = rule.Annualize;
            model.UsesElections = rule.UsesElections;
            model.AllowanceAmount = Text(rule.AllowanceAmount);
            model.SupplementalRate = Text(rule.SupplementalRate);
            model.SupplementalExcessRate = Text(rule.SupplementalExcessRate);
            model.SupplementalExcessThreshold = Text(rule.SupplementalExcessThreshold);

            // Only the methods that work on a table: the rate and amount methods are sources of their own.
            model.Methods = [.. _methods.Methods.Values
                .Where(method => _methodProvider.GetMethod(method.Name) is { } calculation && calculation.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable))
                .Select(method => new SelectListItem(method.DisplayName?.Value ?? method.Name, method.Name, string.Equals(method.Name, rule.Method, StringComparison.OrdinalIgnoreCase)))];
            model.Tables = [.. (await _tables.GetAllAsync())
                .OrderBy(table => table.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(table => new SelectListItem(table.Name, table.ItemId, table.ItemId == rule.TaxTableId))];
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var model = new IncomeTaxTableViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ClearAmounts(rule);
        rule.Method = string.IsNullOrEmpty(model.Method) ? TaxCalculationMethodNames.TaxTable : model.Method;
        rule.TaxTableId = string.IsNullOrEmpty(model.TaxTableId) ? null : model.TaxTableId;
        rule.WageBase = Number(model.WageBase, nameof(model.WageBase), context);
        rule.Annualize = model.Annualize;
        rule.UsesElections = model.UsesElections;
        rule.AllowanceAmount = Number(model.AllowanceAmount, nameof(model.AllowanceAmount), context);
        rule.SupplementalRate = Number(model.SupplementalRate, nameof(model.SupplementalRate), context);
        rule.SupplementalExcessRate = Number(model.SupplementalExcessRate, nameof(model.SupplementalExcessRate), context);
        rule.SupplementalExcessThreshold = Number(model.SupplementalExcessThreshold, nameof(model.SupplementalExcessThreshold), context);

        return Edit(rule, context);
    }
}

/// <summary>
/// A fixed amount rule's fields: the amount each pay and the yearly maximum.
/// </summary>
internal sealed class IncomeTaxFixedAmountDisplayDriver : IncomeTaxRuleSourceDisplayDriver
{
    public IncomeTaxFixedAmountDisplayDriver(IStringLocalizer<IncomeTaxFixedAmountDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    protected override string Source
        => IncomeTaxRuleSources.FixedAmount;

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxFixedAmountViewModel>("IncomeTaxFixedAmount_Edit", model =>
        {
            model.FixedAmount = Text(rule.FixedAmount);
            model.YearlyMaximum = Text(rule.YearlyMaximum);
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var model = new IncomeTaxFixedAmountViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ClearAmounts(rule);
        rule.Method = TaxCalculationMethodNames.FixedAmount;
        rule.FixedAmount = Number(model.FixedAmount, nameof(model.FixedAmount), context);
        rule.YearlyMaximum = Number(model.YearlyMaximum, nameof(model.YearlyMaximum), context);

        return Edit(rule, context);
    }
}

/// <summary>
/// The fields of a rule whose source is a country pack's withholding method (Form W-4, the CRA's formulas, Quebec's
/// TP-1015): the method's own tables and amounts, the person's election, and supplemental income unless the method
/// works it out itself. It edits every source registered with a withholding method, so a pack that adds one gets its
/// editor without writing a driver; a pack that needs more adds its own driver for its source.
/// </summary>
internal sealed class IncomeTaxWithholdingDisplayDriver : IncomeTaxRuleSourceDisplayDriver
{
    private readonly IncomeTaxRuleSourceOptions _sources;
    private readonly IEnumerable<IIncomeTaxWithholdingMethod> _methods;
    private readonly INamedCatalog<TaxTable> _tables;
    private readonly IStringLocalizerFactory _localizerFactory;

    public IncomeTaxWithholdingDisplayDriver(
        IOptions<IncomeTaxRuleSourceOptions> sources,
        IEnumerable<IIncomeTaxWithholdingMethod> methods,
        INamedCatalog<TaxTable> tables,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<IncomeTaxWithholdingDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
        _sources = sources.Value;
        _methods = methods;
        _tables = tables;
        _localizerFactory = localizerFactory;
    }

    // Every source registered with a withholding method, rather than one source.
    protected override string Source
        => null;

    public override bool CanHandleModel(IncomeTaxRule rule)
        => GetMethod(rule) is not null;

    public override IDisplayResult Edit(IncomeTaxRule rule, BuildEditorContext context)
        => Initialize<IncomeTaxWithholdingViewModel>("IncomeTaxWithholding_Edit", async model =>
        {
            var method = GetMethod(rule);

            model.MethodDisplayName = _localizerFactory.Localize(method.DisplayName).Value;
            model.HandlesSupplementalIncome = method.HandlesSupplementalIncome;
            model.TableInputs = [.. method.Tables.Select(table => (table.Key, _localizerFactory.Localize(table.Label).Value, table.Required))];
            model.ParameterInputs = [.. method.Parameters.Select(parameter => (parameter.Key, _localizerFactory.Localize(parameter.Label).Value, parameter.Required))];
            model.Tables = new Dictionary<string, string>(rule.Tables ?? [], StringComparer.Ordinal);
            model.Parameters = (rule.Parameters ?? []).ToDictionary(pair => pair.Key, pair => Text(pair.Value), StringComparer.Ordinal);
            model.UsesElections = rule.UsesElections;
            model.AllowanceAmount = Text(rule.AllowanceAmount);
            model.SupplementalRate = Text(rule.SupplementalRate);
            model.SupplementalExcessRate = Text(rule.SupplementalExcessRate);
            model.SupplementalExcessThreshold = Text(rule.SupplementalExcessThreshold);
            model.AvailableTables = [.. (await _tables.GetAllAsync())
                .OrderBy(table => table.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(table => new SelectListItem(table.Name, table.ItemId))];
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(IncomeTaxRule rule, UpdateEditorContext context)
    {
        var method = GetMethod(rule);
        var model = new IncomeTaxWithholdingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        rule.Method = method.Name;
        rule.Rate = null;
        rule.RateFromRegistration = false;
        rule.FixedAmount = null;
        rule.TaxTableId = null;
        rule.ShareOfTaxCode = null;
        rule.ShareMultiplier = null;
        rule.UsesElections = model.UsesElections;
        rule.AllowanceAmount = Number(model.AllowanceAmount, nameof(model.AllowanceAmount), context);
        rule.SupplementalRate = method.HandlesSupplementalIncome ? null : Number(model.SupplementalRate, nameof(model.SupplementalRate), context);
        rule.SupplementalExcessRate = method.HandlesSupplementalIncome ? null : Number(model.SupplementalExcessRate, nameof(model.SupplementalExcessRate), context);
        rule.SupplementalExcessThreshold = method.HandlesSupplementalIncome ? null : Number(model.SupplementalExcessThreshold, nameof(model.SupplementalExcessThreshold), context);
        rule.Tables = new Dictionary<string, string>(StringComparer.Ordinal);
        rule.Parameters = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var table in method.Tables)
        {
            if (model.Tables?.GetValueOrDefault(table.Key) is { Length: > 0 } tableId)
            {
                rule.Tables[table.Key] = tableId;
            }
        }

        foreach (var parameter in method.Parameters)
        {
            if (Number(model.Parameters?.GetValueOrDefault(parameter.Key), nameof(model.Parameters), context) is { } amount)
            {
                rule.Parameters[parameter.Key] = amount;
            }
        }

        return Edit(rule, context);
    }

    // The withholding method of the rule's source, when its source names one that is on.
    private IIncomeTaxWithholdingMethod GetMethod(IncomeTaxRule rule)
        => rule?.Source is { } name && _sources.Sources.TryGetValue(name, out var source) && source.Method is { } methodName
            ? _methods.FirstOrDefault(method => string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
            : null;
}
