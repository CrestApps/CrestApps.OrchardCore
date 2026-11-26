using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a rotation, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class RotationEditDisplayDriver : DisplayDriver<RotationEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RotationEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RotationEditDisplayDriver(IStringLocalizer<RotationEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(RotationEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<RotationEditViewModel>("RotationDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<RotationEditViewModel>("RotationPattern_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Pattern"], "20")),
            Initialize<RotationEditViewModel>("RotationWorkers_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Workers"], "30")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(RotationEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TeamId, m => m.Enabled, m => m.WeekCount, m => m.WorkerIds, m => m.StartDate);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(RotationEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RotationEditViewModel source, RotationEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.TeamId = source.TeamId;
        model.WeekCount = source.WeekCount;
        model.Slots = source.Slots;
        model.WorkerIds = source.WorkerIds;
        model.StartDate = source.StartDate;
        model.Enabled = source.Enabled;
        model.TeamOptions = source.TeamOptions;
        model.TemplateOptions = source.TemplateOptions;
        model.WorkerOptions = source.WorkerOptions;
        model.Workers = source.Workers;
    }
}
