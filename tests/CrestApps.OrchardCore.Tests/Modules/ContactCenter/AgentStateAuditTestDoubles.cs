using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Builds the real agent state transition point over a recorder a test can read, so a test of any writer can
/// assert exactly what it recorded without the audit adding to the events the writer publishes itself.
/// </summary>
internal static class AgentStateAuditTestDoubles
{
    public static AgentStateTransitionService CreateTransitions(
        IContactCenterAuditRecorder recorder = null,
        IClock clock = null,
        IAgentStateReasonCodeManager reasonCodeManager = null)
    {
        if (clock is null)
        {
            var defaultClock = new Mock<IClock>();
            defaultClock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            clock = defaultClock.Object;
        }

        return new AgentStateTransitionService(
            recorder ?? new RecordingAuditRecorder(),
            reasonCodeManager ?? new Mock<IAgentStateReasonCodeManager>().Object,
            clock,
            NullLogger<AgentStateTransitionService>.Instance);
    }
}

/// <summary>
/// An audit recorder that keeps what it is given, in order.
/// </summary>
internal sealed class RecordingAuditRecorder : IContactCenterAuditRecorder
{
    public List<RecordedAgentStateChange> StateChanges { get; } = [];

    public List<RecordedAgentSessionEvent> SessionEvents { get; } = [];

    public Task RecordAgentStateAsync(AgentStateChangedEventData change, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        StateChanges.Add(new RecordedAgentStateChange(change, actor));

        return Task.CompletedTask;
    }

    public Task RecordAgentSessionAsync(string eventType, AgentSessionEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        SessionEvents.Add(new RecordedAgentSessionEvent(eventType, data, actor));

        return Task.CompletedTask;
    }

    public Task RecordOfferAsync(string eventType, OfferLifecycleEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordCallAsync(
        string eventType,
        CallLifecycleEventData data,
        DateTime occurredUtc,
        ContactCenterActor actor,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordQueueItemWithdrawnAsync(QueueItemWithdrawnEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordSharedVoicemailAsync(string eventType, SharedVoicemailEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed record RecordedAgentStateChange(AgentStateChangedEventData Change, ContactCenterActor Actor);

internal sealed record RecordedAgentSessionEvent(string EventType, AgentSessionEventData Data, ContactCenterActor Actor);
