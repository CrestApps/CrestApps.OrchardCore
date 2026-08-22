using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.StateMachine;

/// <summary>
/// Hosts the production <see cref="ProviderVoiceEventService"/> and the production
/// <see cref="DefaultContactCenterEventPublisher"/> over in-memory stores so randomized delivery sequences
/// can be ingested through the real ingestion, deduplication, staleness and event-resolution code paths.
/// Nothing about the state machine itself is reimplemented here.
/// </summary>
public sealed class CallStateMachineHarness
{
    private readonly Dictionary<string, InteractionEvent> _events = new(StringComparer.Ordinal);
    private readonly List<InteractionEvent> _eventLog = [];
    private readonly ProviderVoiceEventService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallStateMachineHarness"/> class.
    /// </summary>
    /// <param name="providerName">The canonical provider name used for every generated delivery.</param>
    /// <param name="providerCallId">The provider call identifier used for every generated delivery.</param>
    /// <param name="agentId">The agent assigned to the interaction, or <see langword="null"/> for an unassigned call.</param>
    /// <param name="clockUtc">The instant the harness clock reports when a delivery carries no provider timestamp.</param>
    public CallStateMachineHarness(
        string providerName,
        string providerCallId,
        string agentId,
        DateTime clockUtc)
    {
        ProviderName = providerName;
        ProviderCallId = providerCallId;

        Interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = providerName,
            ProviderInteractionId = providerCallId,
            Direction = InteractionDirection.Inbound,
            AgentId = agentId,
        }.RestorePersistedStatus(InteractionStatus.Created);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(clockUtc);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync(providerName, providerCallId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interaction);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync(providerCallId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CallSession { ItemId = "session-1" });
        callSessionManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
            .Callback<CallSession, CancellationToken>((value, _) => Session = value)
            .Returns(ValueTask.CompletedTask);
        callSessionManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback<CallSession, JsonNode, CancellationToken>((value, _, _) => Session = value)
            .Returns(ValueTask.CompletedTask);
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync(providerName, providerCallId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Session);
        callSessionManager
            .Setup(manager => manager.FindByProviderCallIdAsync(providerCallId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Session);
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Session);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                !string.IsNullOrEmpty(key) && _events.ContainsKey(key));
        eventStore
            .Setup(store => store.GetByInteractionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string interactionId, CancellationToken _) =>
                (IReadOnlyList<InteractionEvent>)_eventLog.FindAll(value => string.Equals(value.InteractionId, interactionId, StringComparison.Ordinal)));
        eventStore
            .Setup(store => store.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((value, _) =>
            {
                if (!string.IsNullOrEmpty(value.IdempotencyKey))
                {
                    _events[value.IdempotencyKey] = value;
                }

                _eventLog.Add(value);
            })
            .Returns(ValueTask.CompletedTask);

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit(It.IsAny<Func<ContactCenterEventDispatchContext, Task>>()))
            .Returns(true);

        var publisher = new DefaultContactCenterEventPublisher(
            eventStore.Object,
            new Mock<IContactCenterOutbox>().Object,
            scopeExecutor.Object,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        _service = new ProviderVoiceEventService(
            interactionManager.Object,
            callSessionManager.Object,
            new Mock<IContactCenterVoiceProviderResolver>().Object,
            new Mock<ITelephonyProviderResolver>().Object,
            eventStore.Object,
            publisher,
            new Mock<IAgentPresenceManager>().Object,
            new ProviderIdentityResolver([]),
            new Mock<IProviderCommandStateService>().Object,
            scopeExecutor.Object,
            new Mock<ISession>().Object,
            new VoiceIngressGate(distributedLock.Object),
            clock.Object,
            NullLogger<ProviderVoiceEventService>.Instance);
    }

    /// <summary>
    /// Gets the provider name used for every generated delivery.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the provider call identifier used for every generated delivery.
    /// </summary>
    public string ProviderCallId { get; }

    /// <summary>
    /// Gets the interaction the deliveries resolve to.
    /// </summary>
    public Interaction Interaction { get; }

    /// <summary>
    /// Gets the persisted call session, or <see langword="null"/> when no delivery has created one yet.
    /// </summary>
    public CallSession Session { get; private set; }

    /// <summary>
    /// Gets the durable events the production publisher recorded, in publication order.
    /// </summary>
    public IReadOnlyList<InteractionEvent> PublishedEvents => _eventLog;

    /// <summary>
    /// Ingests one generated delivery through the production ingestion pipeline.
    /// </summary>
    /// <param name="step">The delivery to ingest.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the delivery has been ingested.</returns>
    public Task IngestAsync(CallStateMachineStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        return _service.IngestAsync(step.ToProviderEvent(ProviderName, ProviderCallId), cancellationToken);
    }

    /// <summary>
    /// Counts the durable events of the supplied type that the production publisher recorded.
    /// </summary>
    /// <param name="eventType">The event type to count.</param>
    /// <returns>The number of recorded events of that type.</returns>
    public int CountPublished(string eventType)
    {
        return _eventLog.FindAll(value => string.Equals(value.EventType, eventType, StringComparison.Ordinal)).Count;
    }
}
