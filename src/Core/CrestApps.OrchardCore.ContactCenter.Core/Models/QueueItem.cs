using System.ComponentModel;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Serialization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a CRM activity enqueued and waiting for assignment to an agent.
/// </summary>
public sealed class QueueItem : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the queue that owns the item.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the item represents.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the dialer profile that dials this item, when the item was loaded as
    /// outbound dialer inventory. The profile is chosen at inventory-load time and travels with the item, so
    /// the pacer applies its settings (mode, caller id, compliance) without the profile owning a campaign.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the routing priority of the item. Higher values are handled first.
    /// </summary>
    public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the lifecycle status of the item.
    /// </summary>
    [JsonInclude]
    public QueueItemStatus Status { get; private set; }

    /// <summary>
    /// Moves the QueueItem to the specified lifecycle status.
    /// </summary>
    /// <param name="status">The status to move to.</param>
    /// <exception cref="InvalidStateTransitionException">The QueueItem cannot reach the status from the one it is in.</exception>
    public void TransitionTo(QueueItemStatus status)
    {
        if (!QueueItemLifecycle.CanTransition(Status, status))
        {
            throw new InvalidStateTransitionException(nameof(QueueItem), Status, status);
        }

        Status = status;
    }

    /// <summary>
    /// Determines whether the QueueItem can move to the specified status.
    /// </summary>
    /// <param name="status">The status to test.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public bool CanTransitionTo(QueueItemStatus status)
        => QueueItemLifecycle.CanTransition(Status, status);

    /// <summary>
    /// Gets a value indicating whether the item has left the queue for good.
    /// </summary>
    [JsonIgnore]
    public bool IsSettled => QueueItemLifecycle.IsSettled(Status);

    /// <summary>
    /// Restores a status that was decided elsewhere, without consulting the lifecycle.
    /// </summary>
    /// <param name="status">The status to restore.</param>
    /// <returns>The same QueueItem, so it can be used at the end of an object initializer.</returns>
    /// <remarks>
    /// This bypasses every transition rule and exists only so a test can arrange a state directly. Production code
    /// must never call it: <c>AggregateLifecycleArchitectureTests</c> fails the build if any file under <c>src/</c>
    /// does, so the bypass cannot quietly become a shortcut.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public QueueItem RestorePersistedStatus(QueueItemStatus status)
    {
        Status = status;

        return this;
    }

    /// <summary>
    /// Gets or sets the active reservation identifier when the item is reserved.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent who most recently owned the underlying activity, used as the
    /// sticky-agent preference when the queue enables sticky routing.
    /// </summary>
    public string StickyAgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the agents this item is never offered to, such as the agent who transferred the call into this
    /// queue. Routing and direct offers both skip them; the item waits for somebody else rather than going back.
    /// </summary>
    public IList<string> ExcludedAgentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the agents who declined this item or let its offer ring out, earliest first. Unlike
    /// <see cref="ExcludedAgentIds"/> this is not permanent: routing offers the item to somebody who has not turned it
    /// down yet, and only once everybody who could take it has, starts another round in the order they declined.
    /// </summary>
    public IList<string> DeclinedAgentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the most recent agent in <see cref="DeclinedAgentIds"/> turned the item down.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? LastDeclinedUtc { get; set; }

    /// <summary>
    /// Records that an agent declined the item or let its offer ring out, moving them to the end of
    /// <see cref="DeclinedAgentIds"/> when they had turned it down before.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="declinedUtc">When they turned it down.</param>
    public void RecordDecline(string agentId, DateTime declinedUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        DeclinedAgentIds ??= [];
        DeclinedAgentIds.Remove(agentId);
        DeclinedAgentIds.Add(agentId);
        LastDeclinedUtc = declinedUtc;
    }

    /// <summary>
    /// Gets or sets the identifier of the queue this item overflowed from, when it was moved by overflow handling.
    /// </summary>
    public string OverflowedFromQueueId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifiers this item has already visited through overflow routing.
    /// </summary>
    public IList<string> OverflowHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the agent assigned to the item.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item entered the queue.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime EnqueuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item entered its current queue.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime QueueEnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item left the queue.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? DequeuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item was last modified.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many treatment steps this caller has heard, which is what stops the welcome repeating
    /// every thirty seconds and telling the caller the system has forgotten them.
    /// </summary>
    public int TreatmentStepsPlayed { get; set; }

    /// <summary>
    /// Gets or sets when the caller last heard something, which the announcement cadence is measured from.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? LastTreatmentUtc { get; set; }

    /// <summary>
    /// Gets or sets when the callback was offered, so it is offered once rather than every cycle.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? CallbackOfferedUtc { get; set; }

    /// <summary>
    /// Gets or sets when this item's next overflow hop becomes due, so a scheduler can seek the items that are
    /// ready rather than reading every waiting item every minute.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? OverflowDueUtc { get; set; }

    /// <summary>
    /// Gets or sets when a queued callback was accepted for this caller, which is what stops a repeated key
    /// press or a redelivered provider event producing two calls back.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? CallbackAcceptedUtc { get; set; }
}
