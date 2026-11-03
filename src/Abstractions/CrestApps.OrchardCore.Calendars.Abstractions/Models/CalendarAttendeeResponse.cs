namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// An attendee's answer to an invitation.
/// </summary>
public enum CalendarAttendeeResponse
{
    /// <summary>
    /// The attendee has not answered.
    /// </summary>
    NeedsAction,

    /// <summary>
    /// The attendee will attend.
    /// </summary>
    Accepted,

    /// <summary>
    /// The attendee might attend.
    /// </summary>
    Tentative,

    /// <summary>
    /// The attendee will not attend.
    /// </summary>
    Declined,
}
