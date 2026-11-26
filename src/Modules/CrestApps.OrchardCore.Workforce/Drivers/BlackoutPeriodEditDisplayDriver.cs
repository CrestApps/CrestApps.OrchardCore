using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a blackout period, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class BlackoutPeriodEditDisplayDriver : DisplayDriver<BlackoutPeriodEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackoutPeriodEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BlackoutPeriodEditDisplayDriver(IStringLocalizer<BlackoutPeriodEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(BlackoutPeriodEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<BlackoutPeriodEditViewModel>("BlackoutPeriodDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<BlackoutPeriodEditViewModel>("BlackoutPeriodScope_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Applies To"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(BlackoutPeriodEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.StartDate, m => m.EndDate, m => m.Enabled, m => m.TeamId, m => m.TypeIds, m => m.Message);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(BlackoutPeriodEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(BlackoutPeriodEditViewModel source, BlackoutPeriodEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.TeamId = source.TeamId;
        model.StartDate = source.StartDate;
        model.EndDate = source.EndDate;
        model.TypeIds = source.TypeIds;
        model.Message = source.Message;
        model.Enabled = source.Enabled;
        model.TeamOptions = source.TeamOptions;
        model.TypeOptions = source.TypeOptions;
    }
}
