namespace CrestApps.OrchardCore.ServiceLevels.Core.Models;

/// <summary>
/// One cell of the targets grid: the minutes allowed for a metric at a priority.
/// </summary>
public class ServiceLevelTargetCell
{
    /// <summary>
    /// Gets or sets the metric.
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets the priority; empty for every other priority.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the minutes allowed; empty for no target.
    /// </summary>
    public int? Minutes { get; set; }
}
