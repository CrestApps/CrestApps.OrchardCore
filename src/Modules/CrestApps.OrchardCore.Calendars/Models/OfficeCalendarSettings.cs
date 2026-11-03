namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Settings of an office calendar, kept in the calendar's properties.
/// </summary>
public sealed class OfficeCalendarSettings
{
    /// <summary>
    /// Gets or sets the office's address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets who to contact about the office.
    /// </summary>
    public string Contact { get; set; }
}
