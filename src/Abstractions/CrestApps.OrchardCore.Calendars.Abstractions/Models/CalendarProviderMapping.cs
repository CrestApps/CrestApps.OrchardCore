namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Links an event to its copy in an external calendar.
/// </summary>
public sealed class CalendarProviderMapping
{
    /// <summary>
    /// Gets or sets the id of the connection to the external calendar.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider, such as <c>ics</c>.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the event's id in the external calendar.
    /// </summary>
    public string ExternalEventId { get; set; }

    /// <summary>
    /// Gets or sets the external copy's version marker (an ETag, a change key or a last-modified stamp).
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets when the event was last synchronized with the external copy.
    /// </summary>
    public DateTime? LastSyncedUtc { get; set; }
}
