using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Proves that one provider delivery is ingested once and projected by every consumer.
/// <para>
/// Before the provider-neutral ingress existed, the telephony call-history projection and the Contact Center
/// projection were mutually exclusive alternatives on a first-wins chain. Enabling Contact Center therefore
/// silently stopped telephony call history from ever recording an outcome, and disabling it left the
/// surviving projection running with no lock, no ordering and no de-duplication. These tests wire the real
/// ingestor, the real gate, the real telephony projection and the real
/// <see cref="ProviderVoiceEventService"/> together so both halves of that regression are observable.
/// </para>
/// </summary>
public sealed class VoiceEventFanOutIntegrationTests
{
    private const string _providerName = "Asterisk";
    private const string _providerCallId = "call-1";

    private static readonly ShellSettings _shellSettings = new()
    {
        Name = "TenantA",
    };

    private static readonly DateTime _startedUtc = new(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Dispatch_WhenContactCenterConsumesTheStream_AlsoProjectsTelephonyCallHistory()
    {
        // Arrange
        var context = new FanOutContext();

        // Act
        await context.DispatchAsync(BuildAriSequence());

        // Assert
        Assert.Equal(CallOutcome.Completed, context.TelephonyInteraction.Outcome);
        Assert.Equal(3, context.ContactCenterProjections);
        Assert.Equal(3, context.SoftPhoneProjections.Count);
        Assert.Equal(
            [CallState.Ringing, CallState.Connected, CallState.Disconnected],
            context.SoftPhoneProjections);
    }

    [Fact]
    public async Task Dispatch_ForEachDelivery_TakesExactlyOneIngestionLock()
    {
        // Arrange
        var context = new FanOutContext();
        var sequence = BuildAriSequence();

        // Act
        await context.DispatchAsync(sequence);

        // Assert
        var expectedKey = VoiceIngressKeys.BuildIngestionLockKey(_providerName, _providerCallId);

        Assert.Equal(sequence.Length, context.DistributedLock.AcquiredKeys.Count);
        Assert.All(context.DistributedLock.AcquiredKeys, key => Assert.Equal(expectedKey, key));
    }

    [Fact]
    public async Task Dispatch_ForEachDelivery_WritesExactlyOneDeduplicationRecord()
    {
        // Arrange
        var context = new FanOutContext();
        var sequence = BuildAriSequence();

        // Act
        await context.DispatchAsync(sequence);

        // Assert
        var deliveryKeys = BuildDeliveryDeduplicationKeys(sequence);

        Assert.All(
            deliveryKeys,
            key => Assert.Equal(1, context.DeduplicationRecords.Count(record => string.Equals(record, key, StringComparison.Ordinal))));
        Assert.Equal(
            sequence.Length,
            context.DeduplicationRecords.Count(record => deliveryKeys.Contains(record, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Dispatch_WhenTheProviderRedeliversTheSameEvent_ProjectsItOnlyOnce()
    {
        // Arrange
        var context = new FanOutContext();
        var sequence = BuildAriSequence();

        // Act
        await context.DispatchAsync(sequence);
        await context.DispatchAsync(sequence);

        // Assert
        var deliveryKeys = BuildDeliveryDeduplicationKeys(sequence);

        Assert.Equal(
            sequence.Length,
            context.DeduplicationRecords.Count(record => deliveryKeys.Contains(record, StringComparer.Ordinal)));
        Assert.Equal(sequence.Length, context.ContactCenterProjections);
        Assert.Equal(sequence.Length, context.SoftPhoneProjections.Count);
    }

    [Fact]
    public async Task Dispatch_WhenACallControlBridgeAbsorbsTheEvent_ReachesNoProjection()
    {
        // Arrange
        var absorbingBridge = new Mock<IAsteriskRealtimeVoiceEventBridge>();
        absorbingBridge
            .Setup(bridge => bridge.TryHandleAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new FanOutContext(absorbingBridge.Object);

        // Act
        await context.DispatchAsync(BuildAriSequence());

        // Assert
        Assert.Empty(context.DistributedLock.AcquiredKeys);
        Assert.Empty(context.SoftPhoneProjections);
        Assert.Equal(0, context.ContactCenterProjections);
    }

    [Fact]
    public async Task Dispatch_WhenABridgeThrows_StillRunsTerminalTeardown()
    {
        // Arrange
        var throwingBridge = new Mock<IAsteriskRealtimeVoiceEventBridge>();
        throwingBridge
            .Setup(bridge => bridge.TryHandleAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bridge failure"));

        var teardownService = new Mock<IAsteriskCallTeardownService>();
        var context = new FanOutContext(throwingBridge.Object, teardownService.Object);
        var terminalEvent = BuildVoiceEvent("StasisEnd", CallState.Disconnected, _startedUtc.AddSeconds(65));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.DispatchAsync([terminalEvent]));

        // Assert
        teardownService.Verify(
            service => service.ReleaseAsync(terminalEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Dispatch_WhenABridgeAbsorbsATerminalEvent_StillRunsTerminalTeardown()
    {
        // Arrange
        var absorbingBridge = new Mock<IAsteriskRealtimeVoiceEventBridge>();
        absorbingBridge
            .Setup(bridge => bridge.TryHandleAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var teardownService = new Mock<IAsteriskCallTeardownService>();
        var context = new FanOutContext(absorbingBridge.Object, teardownService.Object);
        var terminalEvent = BuildVoiceEvent("StasisEnd", CallState.Disconnected, _startedUtc.AddSeconds(65));

        // Act
        await context.DispatchAsync([terminalEvent]);

        // Assert
        teardownService.Verify(
            service => service.ReleaseAsync(terminalEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("StasisEnd")]
    [InlineData("ChannelDestroyed")]
    public async Task Dispatch_ForAnEventReportingTheChannelEnded_RunsTerminalTeardown(string eventType)
    {
        // Arrange
        var teardownService = new Mock<IAsteriskCallTeardownService>();
        var context = new FanOutContext(teardownService: teardownService.Object);
        var terminalEvent = BuildVoiceEvent(eventType, CallState.Disconnected, _startedUtc.AddSeconds(65));

        // Act
        await context.DispatchAsync([terminalEvent]);

        // Assert
        teardownService.Verify(
            service => service.ReleaseAsync(terminalEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Dispatch_ForANonTerminalEvent_NeverReachesTheTeardownFanOut()
    {
        // A live channel emits a continuous stream of non-terminal events and ends exactly once, so the dispatcher
        // must not fan out to the teardown services until the channel is actually gone. This asserts the gate lives
        // in the dispatcher rather than depending on each registered implementation to refuse the work itself.

        // Arrange
        var teardownService = new Mock<IAsteriskCallTeardownService>();
        var context = new FanOutContext(teardownService: teardownService.Object);

        var nonTerminalEvents = new[]
        {
            BuildVoiceEvent("StasisStart", CallState.Ringing, _startedUtc),
            BuildVoiceEvent("ChannelStateChange", CallState.Connected, _startedUtc.AddSeconds(5)),
            BuildVoiceEvent("ChannelHangupRequest", CallState.Connected, _startedUtc.AddSeconds(60)),
        };

        // Act
        await context.DispatchAsync(nonTerminalEvents);

        // Assert
        teardownService.Verify(
            service => service.ReleaseAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Dispatch_OverACallLifecycle_RunsTerminalTeardownOnlyForTheEventThatEndedTheChannel()
    {
        // Arrange
        var released = new List<string>();
        var teardownService = new Mock<IAsteriskCallTeardownService>();
        teardownService
            .Setup(service => service.ReleaseAsync(It.IsAny<AsteriskRealtimeVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AsteriskRealtimeVoiceEvent, CancellationToken>((voiceEvent, _) => released.Add(voiceEvent.EventType))
            .Returns(Task.CompletedTask);

        var context = new FanOutContext(teardownService: teardownService.Object);

        // Act
        await context.DispatchAsync(BuildAriSequence());

        // Assert
        Assert.Equal(["StasisEnd"], released);
    }

    private static string[] BuildDeliveryDeduplicationKeys(AsteriskRealtimeVoiceEvent[] sequence)
    {
        return [.. sequence.Select(voiceEvent =>
            VoiceIngressKeys.BuildEventIdempotencyKey(_providerName, voiceEvent.IdempotencyKey))];
    }

    private static AsteriskRealtimeVoiceEvent[] BuildAriSequence()
    {
        return
        [
            BuildVoiceEvent("StasisStart", CallState.Ringing, _startedUtc),
            BuildVoiceEvent("ChannelStateChange", CallState.Connected, _startedUtc.AddSeconds(5)),
            BuildVoiceEvent("StasisEnd", CallState.Disconnected, _startedUtc.AddSeconds(65)),
        ];
    }

    private static AsteriskRealtimeVoiceEvent BuildVoiceEvent(string eventType, CallState state, DateTime occurredUtc)
    {
        return new AsteriskRealtimeVoiceEvent
        {
            ProviderName = _providerName,
            CallId = _providerCallId,
            EventType = eventType,
            State = state,
            FromAddress = "+15550001000",
            ToAddress = "+15550002000",
            OccurredUtc = occurredUtc,
            IdempotencyKey = $"{eventType}:{occurredUtc:O}",
            HangupCause = state == CallState.Disconnected
                ? CrestApps.OrchardCore.Telephony.Models.HangupCause.NormalClearing
                : null,
        };
    }

    private sealed class FanOutContext
    {
        private readonly AsteriskRealtimeVoiceEventDispatcher _dispatcher;

        public FanOutContext(
            IAsteriskRealtimeVoiceEventBridge bridge = null,
            IAsteriskCallTeardownService teardownService = null)
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_startedUtc);

            DistributedLock = new FakeDistributedLock();

            var ingressGate = new VoiceIngressGate(DistributedLock);

            TelephonyInteraction = new TelephonyInteraction
            {
                InteractionId = "telephony-interaction-1",
                CallId = _providerCallId,
                ProviderName = _providerName,
                UserId = "user-1",
                UserName = "mike",
                From = "+15550001000",
                To = "+15550002000",
                Direction = CallDirection.Outbound,
                Outcome = CallOutcome.InProgress,
                StartedUtc = _startedUtc,
            };

            var telephonyInteractionStore = new Mock<ITelephonyInteractionStore>();
            telephonyInteractionStore.SetupRetryingUpdates(TelephonyInteraction);

            var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
            var hubClients = new Mock<IHubClients<ITelephonyClient>>();
            var telephonyClient = new Mock<ITelephonyClient>();

            hubContext.SetupGet(value => value.Clients).Returns(hubClients.Object);
            hubClients
                .Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "user-1")))
                .Returns(telephonyClient.Object);
            telephonyClient
                .Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
                .Callback<TelephonyCall>(call => SoftPhoneProjections.Add(call.State))
                .Returns(Task.CompletedTask);

            var telephonyProjection = new TelephonyCallHistoryVoiceEventHandler(
                telephonyInteractionStore.Object,
                hubContext.Object,
                clock.Object,
                NullLogger<TelephonyCallHistoryVoiceEventHandler>.Instance,
                _shellSettings);

            var contactCenterProjection = new ContactCenterVoiceProjection(
                new ProviderVoiceEventSink(BuildProviderVoiceEventService(clock.Object, ingressGate)),
                NullLogger<ContactCenterVoiceProjection>.Instance);

            var ingestor = new NormalizedVoiceEventIngestor(
                [telephonyProjection, contactCenterProjection],
                new ProviderIdentityResolver([]),
                ingressGate,
                NullLogger<NormalizedVoiceEventIngestor>.Instance);

            _dispatcher = new AsteriskRealtimeVoiceEventDispatcher(
                bridge is null ? [] : [bridge],
                teardownService is null ? [] : [teardownService],
                ingestor,
                NullLogger<AsteriskRealtimeVoiceEventDispatcher>.Instance);
        }

        public FakeDistributedLock DistributedLock { get; }

        public TelephonyInteraction TelephonyInteraction { get; }

        public List<CallState> SoftPhoneProjections { get; } = [];

        public List<string> DeduplicationRecords { get; } = [];

        public int ContactCenterProjections { get; private set; }

        public async Task DispatchAsync(AsteriskRealtimeVoiceEvent[] sequence)
        {
            foreach (var voiceEvent in sequence)
            {
                await _dispatcher.HandleAsync(voiceEvent, TestContext.Current.CancellationToken);
            }
        }

        private ProviderVoiceEventService BuildProviderVoiceEventService(IClock clock, IVoiceIngressGate ingressGate)
        {
            var interaction = new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderName = _providerName,
                ProviderInteractionId = _providerCallId,
                Direction = InteractionDirection.Inbound,
                AgentId = "agent-1",
            }.RestorePersistedStatus(InteractionStatus.Created);

            CallSession session = null;

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync(_providerName, _providerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => interaction);
            interactionManager
                .Setup(manager => manager.FindByProviderInteractionIdAsync(_providerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new CallSession { ItemId = "session-1" });
            callSessionManager
                .Setup(manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
                .Callback<CallSession, CancellationToken>((value, _) => session = value)
                .Returns(ValueTask.CompletedTask);
            callSessionManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Callback<CallSession, JsonNode, CancellationToken>((value, _, _) =>
                {
                    session = value;
                    ContactCenterProjections++;
                })
                .Returns(ValueTask.CompletedTask);
            callSessionManager
                .Setup(manager => manager.FindByProviderCallIdAsync(_providerName, _providerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => session);
            callSessionManager
                .Setup(manager => manager.FindByProviderCallIdAsync(_providerCallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => session);
            callSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => session);

            var eventStore = new Mock<IInteractionEventStore>();
            eventStore
                .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) =>
                    !string.IsNullOrEmpty(key) && DeduplicationRecords.Contains(key, StringComparer.Ordinal));
            eventStore
                .Setup(store => store.GetByInteractionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<InteractionEvent>)[]);
            eventStore
                .Setup(store => store.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((value, _) =>
                {
                    if (!string.IsNullOrEmpty(value.IdempotencyKey))
                    {
                        DeduplicationRecords.Add(value.IdempotencyKey);
                    }
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
                clock,
                NullLogger<DefaultContactCenterEventPublisher>.Instance);

            return new ProviderVoiceEventService(
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
                ingressGate,
                clock,
                NullLogger<ProviderVoiceEventService>.Instance);
        }
    }
}
