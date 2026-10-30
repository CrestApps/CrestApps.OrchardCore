using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// A calendar in the list of calendars.
/// </summary>
public class CalendarRowViewModel
{
    /// <summary>
    /// Gets or sets the calendar.
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// Gets or sets the name of the calendar's type.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the icon of the calendar's type.
    /// </summary>
    public string TypeIcon { get; set; }

    /// <summary>
    /// Gets or sets what the viewer can do with the calendar.
    /// </summary>
    public CalendarAccessLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the owner's name, when the calendar belongs to a user or a team.
    /// </summary>
    public string OwnerName { get; set; }
}
