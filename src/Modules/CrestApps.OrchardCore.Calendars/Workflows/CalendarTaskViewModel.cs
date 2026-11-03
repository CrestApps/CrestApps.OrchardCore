namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// The editor of the calendar workflow tasks. Each task shows the fields it has.
/// </summary>
public class CalendarTaskViewModel
{
    /// <summary>
    /// Gets or sets the Attendees field.
    /// </summary>
    public string Attendees { get; set; }

    /// <summary>
    /// Gets or sets the CalendarId field.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the CalendarIds field.
    /// </summary>
    public string CalendarIds { get; set; }

    /// <summary>
    /// Gets or sets the Description field.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the DurationMinutes field.
    /// </summary>
    public string DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets the End field.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets the EventId field.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the EventType field.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the From field.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the Location field.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the OccurrenceStart field.
    /// </summary>
    public string OccurrenceStart { get; set; }

    /// <summary>
    /// Gets or sets the Reason field.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the Start field.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets the Title field.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the To field.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the UserIds field.
    /// </summary>
    public string UserIds { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes.
    /// </summary>
    public string OutputName { get; set; }
}
