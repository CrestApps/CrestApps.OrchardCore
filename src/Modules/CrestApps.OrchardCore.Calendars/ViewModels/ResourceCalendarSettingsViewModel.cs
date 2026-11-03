namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Details card of a room or equipment calendar.
/// </summary>
public class ResourceCalendarSettingsViewModel
{
    /// <summary>
    /// Gets or sets where the resource is.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets how many people the room seats.
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Gets or sets what the room is equipped with.
    /// </summary>
    public string Amenities { get; set; }
}
