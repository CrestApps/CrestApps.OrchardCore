namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the combined queue usage across the complete filtered report population.
/// </summary>
public sealed class QueueUsageTotals
{
    /// <summary>
    /// Gets or sets the number of interactions routed through queues.
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
    /// Gets or sets the current waiting depth across queues.
    /// </summary>
    public int CurrentWaiting { get; set; }
}
