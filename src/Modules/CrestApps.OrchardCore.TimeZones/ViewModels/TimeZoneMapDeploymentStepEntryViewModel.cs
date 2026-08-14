namespace CrestApps.OrchardCore.TimeZones.ViewModels;

/// <summary>
/// Represents a selectable time zone map entry in a deployment step editor.
/// </summary>
public class TimeZoneMapDeploymentStepEntryViewModel
{
    /// <summary>
    /// Gets or sets the map identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the friendly map name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the mapped time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the map is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
