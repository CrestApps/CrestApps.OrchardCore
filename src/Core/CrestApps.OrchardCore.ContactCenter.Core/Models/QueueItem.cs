using System.ComponentModel;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

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
    public DateTime EnqueuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item entered its current queue.
    /// </summary>
    public DateTime QueueEnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item left the queue.
    /// </summary>
    public DateTime? DequeuedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
