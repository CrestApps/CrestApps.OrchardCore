namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents queue usage aggregated across the queues that currently belong to one queue group.
/// </summary>
public sealed class QueueGroupUsageRow
{
    /// <summary>
    /// Gets or sets the queue-group identifier. An empty value represents queues with no current group.
    /// </summary>
    public string QueueGroupId { get; set; }

    /// <summary>
    /// Gets or sets the resolved queue-group name.
    /// </summary>
    public string QueueGroupName { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions routed through queues in the group.
    /// </summary>
    public long InteractionsHandled { get; set; }

    /// <summary>
    /// Gets or sets the number of answered interactions.
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets the number of abandoned inbound interactions.
    /// </summary>
    public long Abandoned { get; set; }

    /// <summary>
    /// Gets or sets the average handle time, in seconds.
    /// </summary>
    public double AverageHandleTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average speed of answer, in seconds.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the current waiting depth across queues in the group.
    /// </summary>
    public int CurrentWaiting { get; set; }
}
