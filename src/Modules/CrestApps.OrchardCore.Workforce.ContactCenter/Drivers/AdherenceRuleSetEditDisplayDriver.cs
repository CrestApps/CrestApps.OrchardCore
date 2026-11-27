using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of an adherence rule set: its name and teams, then one card for the rule of each kind of activity.
/// </summary>
internal sealed class AdherenceRuleSetEditDisplayDriver : DisplayDriver<AdherenceRuleSetEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceRuleSetEditDisplayDriver(IStringLocalizer<AdherenceRuleSetEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AdherenceRuleSetEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<AdherenceRuleSetEditViewModel>("AdherenceRuleSetGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Rule Set"], "10")),
        };

        for (var i = 0; i < model.Rules.Count; i++)
        {
            var index = i;
            var rule = model.Rules[i];

            // One card for each kind of activity, in the order the rules are posted.
            results.Add(Initialize<AdherenceRuleCardViewModel>("AdherenceRuleSetRule_Edit", m =>
                {
                    m.Index = index;
                    m.Rule = rule;
                })
                .Differentiator("Rule" + index)
                .Location(l => l.Zone("Content", (2 + index).ToString()).Card(AdherenceNames.Kind(S, rule.Kind), (20 + index).ToString())));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AdherenceRuleSetEditViewModel model, UpdateEditorContext context)
    {
        // The rules are posted as Rules[i].Kind, Rules[i].AllowedStatuses...: the page action has bound them already.
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TeamIds);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(AdherenceRuleSetEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(AdherenceRuleSetEditViewModel source, AdherenceRuleSetEditViewModel model)
    {
        model.Name = source.Name;
        model.TeamIds = source.TeamIds;
        model.TeamOptions = source.TeamOptions;
    }
}
