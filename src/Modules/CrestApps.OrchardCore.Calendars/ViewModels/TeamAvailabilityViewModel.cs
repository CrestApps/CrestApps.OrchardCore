using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The page that shows when a team's members are busy on a day, and when they are all free.
/// </summary>
public class TeamAvailabilityViewModel
{
    /// <summary>
    /// Gets or sets the team calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the day, as yyyy-MM-dd.
    /// </summary>
    public string Date { get; set; }

    /// <summary>
    /// Gets or sets how long a meeting must be, in minutes.
    /// </summary>
    public int Minutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the team calendars to choose from.
    /// </summary>
    public IList<SelectListItem> CalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zone the day is shown in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the first hour shown.
    /// </summary>
    public int FirstHour { get; set; } = 7;

    /// <summary>
    /// Gets or sets the hour after the last hour shown.
    /// </summary>
    public int LastHour { get; set; } = 19;

    /// <summary>
    /// Gets or sets each member's busy slots.
    /// </summary>
    public IList<TeamAvailabilityRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the times everyone is free, as wall-clock start and end.
    /// </summary>
    public IList<KeyValuePair<DateTime, DateTime>> FreeTimes { get; set; } = [];
}
