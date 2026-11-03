namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Whose busy time to read, and when.
/// </summary>
public sealed class CalendarBusyQuery
{
    /// <summary>
    /// Gets or sets calendars whose events count, including bookings of resource calendars on other calendars.
    /// </summary>
    public IList<string> CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets users whose time counts: events on calendars they own and events they are invited to and haven't declined.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the start of the range, in UTC.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the range (exclusive), in UTC.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets whether tentative events count as busy.
    /// </summary>
    public bool IncludeTentative { get; set; } = true;

    /// <summary>
    /// Gets or sets events to leave out, such as the event being moved.
    /// </summary>
    public IList<string> ExcludeEventIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to add the busy time from every <see cref="ICalendarBusyTimeSource"/>.
    /// </summary>
    public bool IncludeBusyTimeSources { get; set; } = true;
}
