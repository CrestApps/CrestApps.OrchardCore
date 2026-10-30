namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The details of an occurrence the calendar view shows when it is clicked.
/// </summary>
public sealed class CalendarViewEventDetails
{
    /// <summary>
    /// Gets or sets the event type's name.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Calendar { get; set; }

    /// <summary>
    /// Gets or sets where the event happens.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the event's state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the occurrence belongs to a repeating event.
    /// </summary>
    public bool Recurring { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence was due to start by the series, as an ISO 8601 UTC time.
    /// </summary>
    public string OriginalStart { get; set; }

    /// <summary>
    /// Gets or sets the module that owns the event.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the names of the attendees.
    /// </summary>
    public IList<string> Attendees { get; set; } = [];

    /// <summary>
    /// Gets or sets the address of the event's editor, when the viewer may change it.
    /// </summary>
    public string EditUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may only see busy time.
    /// </summary>
    public bool BusyOnly { get; set; }
}
