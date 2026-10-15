namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

/// <summary>
/// Explains, for one queue, why its pending work is or is not being handed to agents.
/// </summary>
public sealed class QueueDispatchDiagnostics
{
    /// <summary>
    /// Gets or sets the queue identifier; for a campaign, the campaign's own queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier, or <see langword="null"/> for a queue that is not a campaign's.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the number of records pending for the queue: for a campaign, the records that have not been started;
    /// for any other queue, the items waiting.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of pending records the buckets account for. It is lower than <see cref="PendingCount"/>
    /// when the queue holds more records than one pass reads.
    /// </summary>
    public int EvaluatedCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the evaluated records can be handed to an agent right now.
    /// </summary>
    public int DialableNowCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the evaluated records are held back, for any reason.
    /// </summary>
    public int HeldBackCount { get; set; }

    /// <summary>
    /// Gets or sets the evaluated records grouped by what holds them back, largest first, with
    /// <see cref="DispatchBlocker.DialableNow"/> among them when any is dialable.
    /// </summary>
    public IList<DispatchBucket> Buckets { get; set; } = [];

    /// <summary>
    /// Gets or sets the reason the whole queue is paused -- turned off, or its business hours closed -- or
    /// <see langword="null"/> when it is running.
    /// </summary>
    public DispatchBlocker? QueueBlocker { get; set; }

    /// <summary>
    /// Gets or sets the latest routing attempt that found no agent, when nothing has been assigned since.
    /// </summary>
    public DispatchRoutingFinding RoutingFinding { get; set; }
}
