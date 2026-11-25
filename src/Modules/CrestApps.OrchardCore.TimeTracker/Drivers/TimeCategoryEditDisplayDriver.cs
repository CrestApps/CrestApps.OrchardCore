using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TimeTracker.Drivers;

/// <summary>
/// The cards of the form of a category.
/// </summary>
internal sealed class TimeCategoryEditDisplayDriver : DisplayDriver<TimeCategoryEditViewModel>
{
    internal readonly IStringLocalizer S;

    public TimeCategoryEditDisplayDriver(IStringLocalizer<TimeCategoryEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TimeCategoryEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeCategoryEditViewModel>("TimeCategoryGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Category"], "10")),
            Initialize<TimeCategoryEditViewModel>("TimeCategoryReport_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["In The Report"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(TimeCategoryEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Code, m => m.Color, m => m.Position, m => m.Factor, m => m.Enabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeCategoryEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeCategoryEditViewModel source, TimeCategoryEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Code = source.Code;
        model.Color = source.Color;
        model.Position = source.Position;
        model.Factor = source.Factor;
        model.Enabled = source.Enabled;
    }
}
