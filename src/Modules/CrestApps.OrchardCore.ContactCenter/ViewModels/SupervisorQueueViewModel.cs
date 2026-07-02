namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the live operational state of a single queue on the supervisor dashboard.
/// </summary>
public sealed class SupervisorQueueViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the queue.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the queue.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of items currently waiting in the queue.
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// Gets or sets the longest current wait time in the queue, in seconds.
    /// </summary>
    public int LongestWaitSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of waiting items that have breached the queue service-level threshold.
    /// </summary>
    public int SlaBreachCount { get; set; }

    /// <summary>
    /// Gets or sets the queue service-level threshold, in seconds, used to flag breaches.
    /// </summary>
    public int SlaThresholdSeconds { get; set; }
}
