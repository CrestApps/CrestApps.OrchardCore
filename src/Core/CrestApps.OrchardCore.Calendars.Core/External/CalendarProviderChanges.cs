using CrestApps.OrchardCore.Calendars.Core.ICalendar;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// What changed in an external calendar since the last read.
/// </summary>
public sealed class CalendarProviderChanges
{
    /// <summary>
    /// Gets or sets the events that were added or changed. Each <see cref="ICalendarEventData.Uid"/> is the external event's id.
    /// </summary>
    public IList<ICalendarEventData> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the events that were removed.
    /// </summary>
    public IList<string> DeletedIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the token to read the next changes from.
    /// </summary>
    public string NextSyncToken { get; set; }

    /// <summary>
    /// Gets or sets whether the token is no longer valid (a Google Calendar <c>410 Gone</c>, a Microsoft Graph
    /// <c>syncStateNotFound</c>), so the calendar must be read again in full.
    /// </summary>
    public bool FullSyncRequired { get; set; }
}
