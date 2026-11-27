using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time off allotment, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeOffAllotmentEditDisplayDriver : DisplayDriver<TimeOffAllotmentEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAllotmentEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAllotmentEditDisplayDriver(IStringLocalizer<TimeOffAllotmentEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeOffAllotmentEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeOffAllotmentEditViewModel>("TimeOffAllotmentDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<TimeOffAllotmentEditViewModel>("TimeOffAllotmentPlaces_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Places"], "20")),
            Initialize<TimeOffAllotmentEditViewModel>("TimeOffAllotmentApproval_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Approval"], "30")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeOffAllotmentEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TeamId, m => m.TypeIds, m => m.Enabled, m => m.Dates, m => m.AutoApprove, m => m.WhenFull);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeOffAllotmentEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeOffAllotmentEditViewModel source, TimeOffAllotmentEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.TeamId = source.TeamId;
        model.TypeIds = source.TypeIds;
        model.Slots = source.Slots;
        model.Dates = source.Dates;
        model.AutoApprove = source.AutoApprove;
        model.WhenFull = source.WhenFull;
        model.Enabled = source.Enabled;
        model.TeamOptions = source.TeamOptions;
        model.TypeOptions = source.TypeOptions;
    }
}
