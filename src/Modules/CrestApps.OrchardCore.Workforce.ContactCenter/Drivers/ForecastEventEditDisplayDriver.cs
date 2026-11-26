using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of a forecast event, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ForecastEventEditDisplayDriver : DisplayDriver<ForecastEventEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastEventEditDisplayDriver(IStringLocalizer<ForecastEventEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ForecastEventEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ForecastEventEditViewModel>("ForecastEventDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<ForecastEventEditViewModel>("ForecastEventPlanningGroups_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Planning Groups"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ForecastEventEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Kind, m => m.StartDate, m => m.EndDate, m => m.ChangePercent, m => m.RecursYearly, m => m.PlanningGroupIds);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ForecastEventEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ForecastEventEditViewModel source, ForecastEventEditViewModel model)
    {
        model.Name = source.Name;
        model.Kind = source.Kind;
        model.StartDate = source.StartDate;
        model.EndDate = source.EndDate;
        model.ChangePercent = source.ChangePercent;
        model.RecursYearly = source.RecursYearly;
        model.PlanningGroupIds = source.PlanningGroupIds;
        model.PlanningGroupOptions = source.PlanningGroupOptions;
    }
}
