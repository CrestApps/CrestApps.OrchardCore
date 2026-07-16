using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a Contact Center work queue that holds and prioritizes activities waiting for agents.
/// </summary>
public sealed class ActivityQueue : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the optional queue-group identifier used for catalog organization and reporting.
    /// Queue groups do not affect routing, SLA settings, entitlements, or queue behavior.
    /// </summary>
    public string QueueGroupId { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the queue.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the queue.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the default priority applied to items added to the queue.
    /// </summary>
    public InteractionPriority DefaultPriority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the strategy used to choose which available agent receives the next queued item.
    /// </summary>
    public QueueRoutingStrategy RoutingStrategy { get; set; } = QueueRoutingStrategy.LongestIdle;

    /// <summary>
    /// Gets or sets a value indicating whether routing prefers the activity's last assigned user (sticky agent)
    /// when that agent is eligible and available.
    /// </summary>
    public bool PreferStickyAgent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a waiting item's effective priority increases the longer it waits
    /// beyond the SLA threshold, so aging work is routed ahead of newer higher-priority work.
    /// </summary>
    public bool EnableSlaAging { get; set; }

    /// <summary>
    /// Gets or sets the service-level threshold, in seconds, after which a waiting item breaches SLA.
    /// </summary>
    public int SlaThresholdSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the seconds a reservation remains valid before it expires and the item is re-queued.
    /// </summary>
    public int ReservationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets what happens when an offered reservation expires before the agent accepts it.
    /// </summary>
    public UnansweredOfferAction UnansweredOfferAction { get; set; } = UnansweredOfferAction.Requeue;

    /// <summary>
    /// Gets or sets the identifier of the business-hours calendar that gates when the queue routes work.
    /// When empty, the queue routes around the clock.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the action taken for waiting items while the queue's business-hours calendar reports closed.
    /// </summary>
    public QueueAfterHoursAction AfterHoursAction { get; set; } = QueueAfterHoursAction.HoldInQueue;

    /// <summary>
    /// Gets or sets the identifier of the queue that receives overflowed items. When empty, overflow is disabled.
    /// </summary>
    public string OverflowQueueId { get; set; }

    /// <summary>
    /// Gets or sets the seconds an item may wait before it overflows to the overflow queue. Zero disables
    /// wait-time overflow while still allowing after-hours overflow.
    /// </summary>
    public int OverflowAfterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the skills required to be eligible to handle work from this queue.
    /// </summary>
    public IList<string> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the queue is enabled for routing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifier of the inbound channel endpoint (the dialed number or DID) whose
    /// calls are routed to this queue. When set, the inbound voice flow enqueues calls received on
    /// that endpoint into this queue.
    /// </summary>
    public string InboundChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the queue was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the queue was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
