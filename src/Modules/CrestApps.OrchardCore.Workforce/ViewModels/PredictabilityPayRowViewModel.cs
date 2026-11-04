using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A row of the predictability pay list.
/// </summary>
public class PredictabilityPayRowViewModel
{
    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public PredictabilityPayEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone, to show the shift's time.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the user can approve or waive it.
    /// </summary>
    public bool CanDecide { get; set; }
}
