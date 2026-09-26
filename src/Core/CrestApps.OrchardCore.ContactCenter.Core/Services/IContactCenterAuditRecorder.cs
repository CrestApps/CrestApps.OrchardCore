using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The one way agent and call state changes enter the event log for audit, payroll and reporting.
/// </summary>
/// <remarks>
/// Every writer of agent or call state records its change here, so the log's shape, dating and de-duplication are
/// the same whatever code path made the change. Each record is dated by when the change happened (the payload's own
/// time, which is the provider's when it reported one) and carries the time it was recorded, and each is
/// idempotent on what it describes, so a retried webhook or a replayed command never records a change twice.
/// </remarks>
public interface IContactCenterAuditRecorder
{
    /// <summary>
    /// Records an agent state transition as <see cref="ContactCenterConstants.Events.AgentStateChanged"/>.
    /// </summary>
    /// <param name="change">The transition. <see cref="AgentStateChangedEventData.ChangedUtc"/> dates the event.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAgentStateAsync(AgentStateChangedEventData change, ContactCenterActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an agent session event: connected, disconnected or heartbeat lost.
    /// </summary>
    /// <param name="eventType">One of the agent session event types.</param>
    /// <param name="data">The event. <see cref="AgentSessionEventData.OccurredUtc"/> dates it.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAgentSessionAsync(string eventType, AgentSessionEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an offer event.
    /// </summary>
    /// <param name="eventType">One of the offer event types.</param>
    /// <param name="data">The offer. <see cref="OfferLifecycleEventData.SettledUtc"/>, else
    /// <see cref="OfferLifecycleEventData.PresentedUtc"/>, dates it.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordOfferAsync(string eventType, OfferLifecycleEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a call, leg or queued-call event.
    /// </summary>
    /// <param name="eventType">The call event type.</param>
    /// <param name="data">The change.</param>
    /// <param name="occurredUtc">When the change happened: the provider's time when it reported one.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="idempotencyKey">A key naming exactly this change, or <see langword="null"/> to derive one from the
    /// event type, interaction, leg, state and time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordCallAsync(
        string eventType,
        CallLifecycleEventData data,
        DateTime occurredUtc,
        ContactCenterActor actor,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records queued work leaving its queue because its activity stopped being routable, as
    /// <see cref="ContactCenterConstants.Events.QueueItemWithdrawn"/>. A queue item is withdrawn once.
    /// </summary>
    /// <param name="data">The withdrawal. <see cref="QueueItemWithdrawnEventData.WithdrawnUtc"/> dates it.</param>
    /// <param name="actor">Who changed the activity: the user who purged or closed it, or the platform.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordQueueItemWithdrawnAsync(QueueItemWithdrawnEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records something that happened to a message in a queue's shared voicemail box: it arriving, or a user claiming,
    /// releasing, resolving, calling back or deleting it.
    /// </summary>
    /// <param name="eventType">One of the shared voicemail event types.</param>
    /// <param name="data">The change. <see cref="SharedVoicemailEventData.OccurredUtc"/> dates it.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordSharedVoicemailAsync(string eventType, SharedVoicemailEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default);
}
