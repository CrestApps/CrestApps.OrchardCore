using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// An audit recorder that keeps every record it is handed, so a test can say what a writer recorded without a
/// publisher, an event store or a clock.
/// </summary>
public sealed class RecordingContactCenterAuditRecorder : IContactCenterAuditRecorder
{
    /// <summary>
    /// Gets the offer records, in the order they were made.
    /// </summary>
    public List<(string EventType, OfferLifecycleEventData Data, ContactCenterActor Actor)> Offers { get; } = [];

    /// <summary>
    /// Gets the call records, in the order they were made.
    /// </summary>
    public List<(string EventType, CallLifecycleEventData Data, DateTime OccurredUtc, ContactCenterActor Actor, string IdempotencyKey)> Calls { get; } = [];

    /// <inheritdoc/>
    public Task RecordAgentStateAsync(AgentStateChangedEventData change, ContactCenterActor actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task RecordAgentSessionAsync(string eventType, AgentSessionEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task RecordOfferAsync(string eventType, OfferLifecycleEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        Offers.Add((eventType, data, actor));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordCallAsync(
        string eventType,
        CallLifecycleEventData data,
        DateTime occurredUtc,
        ContactCenterActor actor,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((eventType, data, occurredUtc, actor, idempotencyKey));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the queue withdrawal records, in the order they were made.
    /// </summary>
    public List<(QueueItemWithdrawnEventData Data, ContactCenterActor Actor)> Withdrawals { get; } = [];

    /// <inheritdoc/>
    public Task RecordQueueItemWithdrawnAsync(QueueItemWithdrawnEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        Withdrawals.Add((data, actor));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the shared voicemail records, in the order they were made.
    /// </summary>
    public List<(string EventType, SharedVoicemailEventData Data, ContactCenterActor Actor)> SharedVoicemails { get; } = [];

    /// <inheritdoc/>
    public Task RecordSharedVoicemailAsync(string eventType, SharedVoicemailEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        SharedVoicemails.Add((eventType, data, actor));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the call records of one event type.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>The matching records.</returns>
    public List<(string EventType, CallLifecycleEventData Data, DateTime OccurredUtc, ContactCenterActor Actor, string IdempotencyKey)> CallsOf(string eventType)
        => Calls.Where(call => call.EventType == eventType).ToList();
}
