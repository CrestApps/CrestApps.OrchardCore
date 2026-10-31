namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Settings of a room or equipment calendar, kept in the calendar's properties.
/// </summary>
public sealed class ResourceCalendarSettings
{
    /// <summary>
    /// Gets or sets where the resource is, such as "Building A, 2nd floor".
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets how many people the room seats, or 0 when it doesn't apply.
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Gets or sets what the room is equipped with, such as "Projector, whiteboard".
    /// </summary>
    public string Amenities { get; set; }
}
