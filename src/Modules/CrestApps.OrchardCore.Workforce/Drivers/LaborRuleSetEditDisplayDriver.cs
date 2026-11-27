using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a labor rule set, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class LaborRuleSetEditDisplayDriver : DisplayDriver<LaborRuleSetEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRuleSetEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LaborRuleSetEditDisplayDriver(IStringLocalizer<LaborRuleSetEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(LaborRuleSetEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<LaborRuleSetEditViewModel>("LaborRuleSetDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<LaborRuleSetEditViewModel>("LaborRuleSetScope_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Applies To"], "20")),
            Initialize<LaborRuleSetEditViewModel>("LaborRuleSetRules_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Rules"], "30")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(LaborRuleSetEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Jurisdiction, m => m.Description, m => m.Enabled, m => m.TeamIds, m => m.EmploymentTypes, m => m.WorkerIds, m => m.AddRuleKey);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(LaborRuleSetEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(LaborRuleSetEditViewModel source, LaborRuleSetEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Description = source.Description;
        model.Jurisdiction = source.Jurisdiction;
        model.TeamIds = source.TeamIds;
        model.EmploymentTypes = source.EmploymentTypes;
        model.Enabled = source.Enabled;
        model.Rules = source.Rules;
        model.AddRuleKey = source.AddRuleKey;
        model.WorkerCount = source.WorkerCount;
        model.WorkerIds = source.WorkerIds;
        model.WorkerOptions = source.WorkerOptions;
        model.Catalog = source.Catalog;
        model.RuleOptions = source.RuleOptions;
        model.TeamOptions = source.TeamOptions;
        model.EmploymentOptions = source.EmploymentOptions;
    }
}
