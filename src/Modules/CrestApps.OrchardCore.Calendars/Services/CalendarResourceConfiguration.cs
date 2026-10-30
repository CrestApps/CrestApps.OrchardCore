using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Registers the calendar view's script and style sheet.
/// </summary>
internal sealed class CalendarResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the script that draws the calendar view.
    /// </summary>
    public const string ViewScript = "CrestApps.OrchardCore.Calendars.View";

    /// <summary>
    /// The name of the calendar view's style sheet.
    /// </summary>
    public const string ViewStyle = "CrestApps.OrchardCore.Calendars.View";

    private static readonly ResourceManifest _manifest;

    static CalendarResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(ViewScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Calendars/scripts/calendar-view.min.js",
                "~/CrestApps.OrchardCore.Calendars/scripts/calendar-view.js")
            .SetDependencies("fullcalendar")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(ViewStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Calendars/styles/calendar-view.min.css",
                "~/CrestApps.OrchardCore.Calendars/styles/calendar-view.css")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
