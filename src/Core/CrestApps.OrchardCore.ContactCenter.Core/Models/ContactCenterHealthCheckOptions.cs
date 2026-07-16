namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Configures the thresholds used by the Contact Center operational health checks. Defaults are tuned for a
/// single healthy node; operators raise them for larger deployments through configuration.
/// </summary>
public sealed class ContactCenterHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the dead-letter count at or above which a queue is reported as degraded. A single
    /// dead-lettered message already signals a delivery failure requiring operator attention.
    /// </summary>
    public int DeadLetterDegradedThreshold { get; set; } = 1;

    /// <summary>
    /// Gets or sets the dead-letter count at or above which a queue is reported as unhealthy.
    /// </summary>
    public int DeadLetterUnhealthyThreshold { get; set; } = 25;

    /// <summary>
    /// Gets or sets the overdue backlog size at or above which a queue is reported as degraded. A sustained
    /// overdue backlog indicates the background dispatcher is not keeping up.
    /// </summary>
    public int OverdueBacklogDegradedThreshold { get; set; } = 50;

    /// <summary>
    /// Gets or sets the overdue backlog size at or above which a queue is reported as unhealthy.
    /// </summary>
    public int OverdueBacklogUnhealthyThreshold { get; set; } = 500;

    /// <summary>
    /// Normalizes the configured thresholds so a lower unhealthy bound can never sit below its degraded bound.
    /// </summary>
    public void Normalize()
    {
        DeadLetterDegradedThreshold = Math.Max(1, DeadLetterDegradedThreshold);
        DeadLetterUnhealthyThreshold = Math.Max(DeadLetterDegradedThreshold, DeadLetterUnhealthyThreshold);
        OverdueBacklogDegradedThreshold = Math.Max(1, OverdueBacklogDegradedThreshold);
        OverdueBacklogUnhealthyThreshold = Math.Max(OverdueBacklogDegradedThreshold, OverdueBacklogUnhealthyThreshold);
    }
}
