namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Queued work taken out of its queue because its activity stopped being routable: the payload of
/// <see cref="ContactCenterConstants.Events.QueueItemWithdrawn"/>.
/// </summary>
public sealed class QueueItemWithdrawnEventData
{
    /// <summary>
    /// Gets or sets the queue item that was withdrawn.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue the item was waiting in.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the activity the queued work was for.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the queued work carried, when it had one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the status the queue item was in before it was withdrawn.
    /// </summary>
    public string PreviousState { get; set; }

    /// <summary>
    /// Gets or sets the offer that was revoked because of the withdrawal, when the work was ringing an agent.
    /// </summary>
    public string RevokedReservationId { get; set; }

    /// <summary>
    /// Gets or sets the agent whose offer was revoked, when there was one.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets why the work was withdrawn: what happened to its activity.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets how long the work had waited in the queue, in seconds.
    /// </summary>
    public double WaitSeconds { get; set; }

    /// <summary>
    /// Gets or sets when the work was withdrawn.
    /// </summary>
    public DateTime WithdrawnUtc { get; set; }
}
