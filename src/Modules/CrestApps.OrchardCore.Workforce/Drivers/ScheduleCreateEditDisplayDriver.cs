using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a schedule create, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ScheduleCreateEditDisplayDriver : DisplayDriver<ScheduleCreateViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleCreateEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ScheduleCreateEditDisplayDriver(IStringLocalizer<ScheduleCreateEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ScheduleCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ScheduleCreateViewModel>("ScheduleCreateDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Schedule"], "10")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ScheduleCreateViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.TeamId, m => m.StartDate, m => m.Weeks, m => m.Name);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ScheduleCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ScheduleCreateViewModel source, ScheduleCreateViewModel model)
    {
        model.TeamId = source.TeamId;
        model.Name = source.Name;
        model.StartDate = source.StartDate;
        model.Weeks = source.Weeks;
        model.TeamOptions = source.TeamOptions;
    }
}
