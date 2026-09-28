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
    /// Gets or sets a value indicating whether readiness may drain this node after consecutive failures of a
    /// node-local store probe.
    /// </summary>
    /// <remarks>
    /// Disabled by default. When the store itself is down every node observes the same failure, so the gate
    /// drains the whole fleet and a degraded dependency becomes a total outage. Enable it only where the load
    /// balancer fails open once too few targets remain healthy, or where partial drain is preferable to partial
    /// failure. When disabled the check performs no I/O.
    /// </remarks>
    public bool EnableNodeServingGate { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failed store probes required before the node reports that it
    /// cannot serve. Only used when <see cref="EnableNodeServingGate"/> is enabled.
    /// </summary>
    public int ConsecutiveFailuresBeforeUnready { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of consecutive successful store probes required before a draining node reports
    /// that it can serve again. Only used when <see cref="EnableNodeServingGate"/> is enabled.
    /// </summary>
    public int ConsecutiveSuccessesBeforeReady { get; set; } = 2;

    /// <summary>
    /// Normalizes the configured thresholds so a lower unhealthy bound can never sit below its degraded bound.
    /// </summary>
    public void Normalize()
    {
        DeadLetterDegradedThreshold = Math.Max(1, DeadLetterDegradedThreshold);
        DeadLetterUnhealthyThreshold = Math.Max(DeadLetterDegradedThreshold, DeadLetterUnhealthyThreshold);
        OverdueBacklogDegradedThreshold = Math.Max(1, OverdueBacklogDegradedThreshold);
        OverdueBacklogUnhealthyThreshold = Math.Max(OverdueBacklogDegradedThreshold, OverdueBacklogUnhealthyThreshold);
        ConsecutiveFailuresBeforeUnready = Math.Max(1, ConsecutiveFailuresBeforeUnready);
        ConsecutiveSuccessesBeforeReady = Math.Max(1, ConsecutiveSuccessesBeforeReady);
    }
}
