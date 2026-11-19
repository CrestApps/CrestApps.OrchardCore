using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// The permissions of the Timeline building block. Who may read and write the timeline of a record is decided by the
/// module that owns the record, through its <see cref="Services.ITimelineAccessProvider"/>.
/// </summary>
public static class TimelinePermissions
{
    /// <summary>
    /// Lets a user redact any timeline entry, whatever record it is on.
    /// </summary>
    public static readonly Permission RedactTimelineEntries = new(
        "RedactTimelineEntries",
        LocalizationSource.Create("Redact timeline entries", typeof(TimelinePermissions)));
}
