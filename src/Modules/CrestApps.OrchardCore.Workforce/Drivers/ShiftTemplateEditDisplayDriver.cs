using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a shift template, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ShiftTemplateEditDisplayDriver : DisplayDriver<ShiftTemplateEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTemplateEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftTemplateEditDisplayDriver(IStringLocalizer<ShiftTemplateEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ShiftTemplateEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ShiftTemplateEditViewModel>("ShiftTemplateShift_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Shift"], "10")),
            Initialize<ShiftTemplateEditViewModel>("ShiftTemplateEligibility_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Who Can Work It"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ShiftTemplateEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Start, m => m.End, m => m.Segments, m => m.Color, m => m.Enabled, m => m.TeamIds, m => m.RequiredSkillIds);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ShiftTemplateEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ShiftTemplateEditViewModel source, ShiftTemplateEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Color = source.Color;
        model.Start = source.Start;
        model.End = source.End;
        model.Segments = source.Segments;
        model.TeamIds = source.TeamIds;
        model.RequiredSkillIds = source.RequiredSkillIds;
        model.Enabled = source.Enabled;
        model.ActivityNames = source.ActivityNames;
        model.TeamOptions = source.TeamOptions;
        model.SkillOptions = source.SkillOptions;
    }
}
