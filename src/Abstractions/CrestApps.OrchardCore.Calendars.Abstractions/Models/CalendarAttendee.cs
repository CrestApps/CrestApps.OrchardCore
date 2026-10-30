namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A person or resource invited to an event: a user, an outside person by email, or a resource calendar.
/// </summary>
public sealed class CalendarAttendee
{
    /// <summary>
    /// Gets or sets the attendee's user id, for a user of the site.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the attendee's email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the attendee's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the id of a resource calendar (a room or a piece of equipment) the event uses.
    /// </summary>
    public string ResourceCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the part the attendee plays.
    /// </summary>
    public CalendarAttendeeRole Role { get; set; }

    /// <summary>
    /// Gets or sets the attendee's answer.
    /// </summary>
    public CalendarAttendeeResponse Response { get; set; }

    /// <summary>
    /// Gets or sets when the attendee answered.
    /// </summary>
    public DateTime? RespondedUtc { get; set; }

    /// <summary>
    /// Gets or sets the secret that lets an outside attendee answer from an invitation link.
    /// </summary>
    public string ResponseToken { get; set; }

    /// <summary>
    /// Gets the key that identifies the attendee within an event: the resource calendar id, the user id or the email in lower case.
    /// </summary>
    /// <returns>The attendee's key, or <see langword="null"/> when the attendee names nobody.</returns>
    public string GetKey()
    {
        if (!string.IsNullOrEmpty(ResourceCalendarId))
        {
            return "resource:" + ResourceCalendarId;
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            return "user:" + UserId;
        }

        if (!string.IsNullOrWhiteSpace(Email))
        {
            return "email:" + Email.Trim().ToLowerInvariant();
        }

        return null;
    }
}
