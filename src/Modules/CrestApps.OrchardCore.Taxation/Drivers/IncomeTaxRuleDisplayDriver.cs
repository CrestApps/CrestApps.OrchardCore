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
/// The rows of income tax rules, and the fields every rule has whatever its source: what the tax is, where it applies,
/// who pays and when. How the tax is worked out comes from the driver of the rule's source.
/// </summary>
internal sealed class IncomeTaxRuleDisplayDriver : DisplayDriver<IncomeTaxRule>
{
    private readonly IncomeTaxCategoryOptions _categories;
    private readonly IncomeTaxRuleSourceOptions _sources;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public IncomeTaxRuleDisplayDriver(
        IOptions<IncomeTaxCategoryOptions> categories,
        IOptions<IncomeTaxRuleSourceOptions> sources,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<IncomeTaxRuleDisplayDriver> stringLocalizer)
    {
        _categories = categories.Value;
        _sources = sources.Value;
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
    {
        return Combine(
            Initialize<IncomeTaxRuleViewModel>("IncomeTaxRuleFields_Edit", model => Fill(model, rule, context)).Location("Content:1"),
            Initialize<IncomeTaxRuleViewModel>("IncomeTaxRuleWhen_Edit", model => Fill(model, rule, context)).Location("Content:20"));
    }

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
        rule.EffectiveTo = model.EffectiveTo;
        rule.Enabled = model.Enabled;

        // An empty first day is the handler's to refuse, as for a recipe.
        rule.EffectiveFrom = model.EffectiveFrom ?? default;

        return Edit(rule, context);
    }

    private void Fill(IncomeTaxRuleViewModel model, IncomeTaxRule rule, BuildEditorContext context)
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
        model.EffectiveFrom = context.IsNew && rule.EffectiveFrom == default ? null : rule.EffectiveFrom;
        model.EffectiveTo = rule.EffectiveTo;
        model.Enabled = rule.Enabled;

        if (rule.Source is { } name && _sources.Sources.TryGetValue(name, out var source))
        {
            model.SourceDisplayName = _localizerFactory.Localize(source.DisplayName).Value;
            model.SourceDescription = source.Description is null ? null : _localizerFactory.Localize(source.Description).Value;

            // Values a rule of an older layout holds that its kind doesn't: saving drops them, so the page says which.
            model.DroppedOnSave = context.IsNew ? [] : [.. IncomeTaxRuleSources.ValuesNotOwned(rule, source.Fields)];
        }
        else
        {
            // A source whose feature is off: its fields aren't shown, and the rule keeps them as they are.
            model.SourceDisplayName = rule.Source;
            model.SourceDescription = S["The feature that adds this kind of rule is off, so how the tax is worked out can't be edited here."];
        }

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
    }
}
