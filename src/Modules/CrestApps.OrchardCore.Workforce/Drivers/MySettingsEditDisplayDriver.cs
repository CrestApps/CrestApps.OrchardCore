using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the form of what a worker hears about in the My Work portal, grouped in cards by their placement. The
/// controller reads the posted values itself, so this driver only builds the editor.
/// </summary>
internal sealed class MySettingsEditDisplayDriver : DisplayDriver<MySettingsViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySettingsEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MySettingsEditDisplayDriver(IStringLocalizer<MySettingsEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(MySettingsViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<MySettingsViewModel>("MySettingsChannels_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Channels"], "10")),
            Initialize<MySettingsViewModel>("MySettingsKinds_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Topics"], "20")),
            Initialize<MySettingsViewModel>("MySettingsReminders_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Reminders"], "30")),
            Initialize<MySettingsViewModel>("MySettingsQuietHours_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Quiet Hours"], "40")));
    }

    // The fields keep the names the page posts, which the controller reads as its own parameters.
    protected override void BuildPrefix(MySettingsViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(MySettingsViewModel source, MySettingsViewModel model)
    {
        model.Channels = source.Channels;
        model.Kinds = source.Kinds;
        model.ReminderMinutes = source.ReminderMinutes;
        model.QuietStart = source.QuietStart;
        model.QuietEnd = source.QuietEnd;
    }
}
