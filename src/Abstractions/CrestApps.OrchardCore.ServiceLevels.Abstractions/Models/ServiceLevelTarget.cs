namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// The time a policy allows for one metric at one priority.
/// </summary>
public sealed class ServiceLevelTarget
{
    /// <summary>
    /// Gets or sets the metric, one of <see cref="ServiceLevelMetrics"/> or a metric the policy's scope adds.
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets the priority the target is for, as the policy's scope keys its priorities. Empty means every
    /// priority that has no target of its own.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the time allowed, in minutes of the policy's hours.
    /// </summary>
    public int Minutes { get; set; }
}
