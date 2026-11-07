namespace CrestApps.OrchardCore.BusinessHours.Models;

/// <summary>
/// The hours something is open every week, in a time zone, with the dates that differ: holidays closed all day and
/// days with special hours. Every module that asks "are we open?" reads one of these through
/// <see cref="Services.IOpenHoursService"/> instead of keeping its own weekly hours and holiday list.
/// </summary>
public sealed class HoursSchedule
{
    /// <summary>
    /// Gets or sets the IANA or Windows time zone the hours and dates are in; empty or unknown means UTC.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the open windows of the week. A day can have several windows (a lunch break), or none (closed).
    /// </summary>
    public IList<HoursWindow> Windows { get; set; } = [];

    /// <summary>
    /// Gets or sets the dates that don't follow the weekly windows: holidays and days with special hours.
    /// </summary>
    public IList<HoursDateException> DateExceptions { get; set; } = [];
}
