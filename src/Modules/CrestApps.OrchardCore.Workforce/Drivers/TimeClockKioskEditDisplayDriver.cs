using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time clock kiosk, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeClockKioskEditDisplayDriver : DisplayDriver<TimeClockKioskEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockKioskEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockKioskEditDisplayDriver(IStringLocalizer<TimeClockKioskEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeClockKioskEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeClockKioskEditViewModel>("TimeClockKioskDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Kiosk"], "10")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeClockKioskEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TeamId, m => m.Enabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeClockKioskEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeClockKioskEditViewModel source, TimeClockKioskEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.TeamId = source.TeamId;
        model.Enabled = source.Enabled;
        model.NewAddress = source.NewAddress;
        model.TeamOptions = source.TeamOptions;
    }
}
