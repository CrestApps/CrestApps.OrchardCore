using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.TimeZones.ViewModels;

/// <summary>
/// Represents the editor view model for a time zone map.
/// </summary>
public class TimeZoneMapViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the map is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the unique friendly name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the selected Orchard Core time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the available time zones for selection.
    /// </summary>
    public IEnumerable<SelectListItem> TimeZones { get; set; } = [];
}
