namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// The calendar event an activity was placed on, kept on the activity so the two stay in step.
/// </summary>
public sealed class ActivityCalendarLink
{
    /// <summary>
    /// Gets or sets the calendar event, or <see langword="null"/> when the activity is no longer on a calendar.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the calendar the event is on.
    /// </summary>
    public string CalendarId { get; set; }
}
