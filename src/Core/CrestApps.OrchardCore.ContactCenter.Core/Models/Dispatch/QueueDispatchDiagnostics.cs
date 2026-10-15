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
    /// Gets or sets the number of queue items waiting in the queue, whatever their activity's state.
    /// </summary>
    public int WaitingItemCount { get; set; }

    /// <summary>
    /// Gets or sets the number of queue items reserved for an agent and awaiting acceptance.
    /// </summary>
    public int ReservedItemCount { get; set; }

    /// <summary>
    /// Gets or sets the number of queue items assigned to an agent.
    /// </summary>
    public int AssignedItemCount { get; set; }

    /// <summary>
    /// Gets or sets how the queue is paced, from the dialer profile stamped on its head item. It is meaningful for a
    /// campaign's queue only.
    /// </summary>
    public DispatchPacing Pacing { get; set; }

    /// <summary>
    /// Gets or sets the dialer profiles stamped on the waiting items, most items first.
    /// </summary>
    public IList<DispatchProfileStamp> ProfileStamps { get; set; } = [];

    /// <summary>
    /// Gets or sets the evaluated pending activities grouped by source, interaction type and channel, largest first.
    /// </summary>
    public IList<DispatchActivityGroup> ActivityMix { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of evaluated waiting items whose stamped dialer profile differs from the one their activity
    /// carries. The queue gate trusts the stamp, so a disagreement means the activity is dialed under a profile it was not
    /// given.
    /// </summary>
    public int ProfileMismatchCount { get; set; }

    /// <summary>
    /// Gets or sets the number of evaluated pending activities that carry no dialer profile of their own.
    /// </summary>
    public int ActivitiesWithoutProfileCount { get; set; }

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
