namespace CrestApps.OrchardCore.Calendars.Api;

/// <summary>
/// A free/busy question sent to the API.
/// </summary>
public sealed class CalendarFreeBusyInput
{
    /// <summary>
    /// Gets or sets the calendars whose time counts.
    /// </summary>
    public IList<string> CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the users whose time counts.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the start of the range.
    /// </summary>
    public DateTimeOffset From { get; set; }

    /// <summary>
    /// Gets or sets the end of the range; at most 62 days after the start.
    /// </summary>
    public DateTimeOffset To { get; set; }

    /// <summary>
    /// Gets or sets how long a free time must be, in minutes, for the free-time call.
    /// </summary>
    public int DurationMinutes { get; set; } = 30;
}
