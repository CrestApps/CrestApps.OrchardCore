using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of a new forecast, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ForecastCreateDisplayDriver : DisplayDriver<ForecastCreateViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastCreateDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastCreateDisplayDriver(IStringLocalizer<ForecastCreateDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ForecastCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ForecastCreateViewModel>("ForecastCreateDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ForecastCreateViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.PlanningGroupId, m => m.FromDate, m => m.Weeks, m => m.Model);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ForecastCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ForecastCreateViewModel source, ForecastCreateViewModel model)
    {
        model.PlanningGroupId = source.PlanningGroupId;
        model.FromDate = source.FromDate;
        model.Weeks = source.Weeks;
        model.Model = source.Model;
        model.PlanningGroupOptions = source.PlanningGroupOptions;
    }
}
