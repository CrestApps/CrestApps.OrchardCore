namespace CrestApps.OrchardCore.TimeZones.ViewModels;

/// <summary>
/// Represents the editor view model for the time zone map deployment step.
/// </summary>
public class TimeZoneMapDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether all maps should be included.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the available time zone maps.
    /// </summary>
    public TimeZoneMapDeploymentStepEntryViewModel[] Maps { get; set; }
}
