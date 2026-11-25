using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TimeTracker.Drivers;

/// <summary>
/// The cards of the form of a stage.
/// </summary>
internal sealed class TimeStageEditDisplayDriver : DisplayDriver<TimeStageEditViewModel>
{
    internal readonly IStringLocalizer S;

    public TimeStageEditDisplayDriver(IStringLocalizer<TimeStageEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TimeStageEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeStageEditViewModel>("TimeStageGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Stage"], "10")),
            Initialize<TimeStageEditViewModel>("TimeStageCategories_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["How Its Time Counts"], "20")),
            Initialize<TimeStageEditViewModel>("TimeStageUse_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Use"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(TimeStageEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Code, m => m.Color, m => m.Position, m => m.CategoryIds, m => m.SelfService, m => m.Enabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeStageEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeStageEditViewModel source, TimeStageEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Code = source.Code;
        model.Color = source.Color;
        model.Position = source.Position;
        model.CategoryIds = source.CategoryIds;
        model.SelfService = source.SelfService;
        model.Enabled = source.Enabled;
        model.CategoryOptions = source.CategoryOptions;
    }
}
