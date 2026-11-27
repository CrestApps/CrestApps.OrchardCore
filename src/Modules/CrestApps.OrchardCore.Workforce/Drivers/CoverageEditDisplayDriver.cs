using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a coverage requirement, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class CoverageEditDisplayDriver : DisplayDriver<CoverageEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CoverageEditDisplayDriver(IStringLocalizer<CoverageEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(CoverageEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CoverageEditViewModel>("CoverageDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<CoverageEditViewModel>("CoverageNeeds_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Staffing Needs"], "20")),
            Initialize<CoverageEditViewModel>("CoverageDates_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Dates"], "30")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(CoverageEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TeamId, m => m.SkillId, m => m.Rows, m => m.EffectiveFrom, m => m.EffectiveTo);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(CoverageEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CoverageEditViewModel source, CoverageEditViewModel model)
    {
        model.Id = source.Id;
        model.TeamId = source.TeamId;
        model.Name = source.Name;
        model.SkillId = source.SkillId;
        model.EffectiveFrom = source.EffectiveFrom;
        model.EffectiveTo = source.EffectiveTo;
        model.Rows = source.Rows;
        model.TeamOptions = source.TeamOptions;
        model.SkillOptions = source.SkillOptions;
    }
}
