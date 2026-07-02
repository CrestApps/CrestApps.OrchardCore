namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the usage of a single queue over a reporting period, combining historical handled volume
/// with the current live waiting depth.
/// </summary>
public sealed class QueueUsageRow
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the resolved queue name.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions that were routed through the queue in the period.
    /// </summary>
    public long InteractionsHandled { get; set; }

    /// <summary>
    /// Gets or sets the number of the queue's interactions that were answered.
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets the number of the queue's inbound interactions abandoned before an agent answered.
    /// </summary>
    public long Abandoned { get; set; }

    /// <summary>
    /// Gets or sets the average handle time, in seconds, for the queue's answered interactions.
    /// </summary>
    public double AverageHandleTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average speed of answer, in seconds, for the queue's answered interactions.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of items currently waiting in the queue.
    /// </summary>
    public int CurrentWaiting { get; set; }

    /// <summary>
    /// Gets or sets the service-level threshold, in seconds, configured for the queue.
    /// </summary>
    public int SlaThresholdSeconds { get; set; }
}
