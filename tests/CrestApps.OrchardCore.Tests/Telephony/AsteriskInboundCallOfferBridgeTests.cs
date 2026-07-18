using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskInboundCallOfferBridgeTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TryHandleAsync_WhenEventIsNotInbound_ReturnsFalseWithoutCallingAriOrSink()
    {
        // Arrange
        var bindingStore = new TestBindingStore();
        var ariClient = new TestAriClient();
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(new AsteriskRealtimeVoiceEvent
        {
            IsInbound = false,
            ChannelId = "channel-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
        Assert.Equal(0, bindingStore.FindCount);
        Assert.Empty(ariClient.Calls);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenInboundBindingAlreadyExists_ReturnsTrueWithoutDuplicateWork()
    {
        // Arrange
        var bindingStore = new TestBindingStore
        {
            ExistingBinding = new AsteriskChannelTenantBinding
            {
                ChannelId = "channel-1",
            },
        };
        var ariClient = new TestAriClient();
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Empty(ariClient.Calls);
        Assert.Null(sink.RoutedEvent);
        Assert.Null(bindingStore.CreatedBinding);
    }

    [Fact]
    public async Task TryHandleAsync_WhenInboundBindingDoesNotExist_AnswersParksBindsAndRoutes()
    {
        // Arrange
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls);
        var ariClient = new TestAriClient(calls);
        var sink = new TestInboundVoiceEventSink(calls);
        var bridge = CreateBridge(bindingStore, ariClient, sink);
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "channel-1";

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);

        // The binding is persisted BEFORE any ARI side effect so a crash after answering/parking always leaves a
        // durable recovery record for the reconciler; the binding is the idempotency claim and recovery anchor. It is
        // created in the Offering provisioning state and only promoted to Connected once routing succeeds, so a crash
        // before RouteAsync leaves an Offering record the reconciler can age-terminate instead of a false-healthy leg.
        Assert.Equal(
        [
            "find:channel-1",
            "create-binding:channel-1",
            "answer:channel-1",
            $"create-bridge:{holdingBridgeId}:{AsteriskAriConstants.HoldingBridgeType}",
            $"add-channel:{holdingBridgeId}:channel-1",
            "route:call-1",
            "promote-offering:channel-1",
        ], calls);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.Equal("channel-1", bindingStore.CreatedBinding.ChannelId);
        Assert.Equal(AsteriskChannelBindingState.Connected, bindingStore.CreatedBinding.State);
        Assert.Equal("Asterisk", bindingStore.CreatedBinding.ProviderName);
        Assert.Equal("call-1", bindingStore.CreatedBinding.ProviderCallId);
        Assert.Equal("interaction-1", bindingStore.CreatedBinding.InteractionId);
        Assert.Equal(_now, bindingStore.CreatedBinding.CreatedUtc);
        Assert.NotNull(sink.RoutedEvent);
        Assert.Equal("+15550001000", sink.RoutedEvent.FromAddress);
        Assert.Equal("+15551234567", sink.RoutedEvent.ToAddress);
        Assert.Equal(_now, sink.RoutedEvent.ReceivedUtc);
    }

    [Fact]
    public async Task TryHandleAsync_WhenAnswerThrows_HangsUpPossiblyAnsweredCallerThenRemovesBinding()
    {
        // Arrange
        // A thrown answer may be a LOST ACK: Asterisk answered the caller but the response never returned, so the
        // caller can be live server-side. The failure path must treat the attempted answer as possibly-answered and
        // hang the caller up (compensation) rather than strand an answered caller in silence. Because the hangup
        // succeeds here, cleanup is certain and the durable binding is removed after compensation.
        var bindingStore = new TestBindingStore();
        var ariClient = new TestAriClient
        {
            ThrowOnAnswer = true,
        };
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Contains("hangup:channel-1", ariClient.Calls);
        Assert.Equal("channel-1", bindingStore.RemovedChannelId);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenAnswerThrowsAndHangupFails_RetainsBindingForReconciler()
    {
        // Arrange
        // The answer ack was lost (the caller may be live server-side) AND the compensating hangup also fails, so
        // cleanup is uncertain. The durable Offering binding must be RETAINED so the reconciler can resolve the aged
        // record instead of deleting the only record that tracks a caller potentially still live on Asterisk.
        var bindingStore = new TestBindingStore();
        var ariClient = new TestAriClient
        {
            ThrowOnAnswer = true,
            ThrowOnHangup = true,
        };
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Contains("hangup:channel-1", ariClient.Calls);
        Assert.Null(bindingStore.RemovedChannelId);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenRoutingProducesNoInteraction_TerminatesOfferAndNeverPromotes()
    {
        // Arrange
        // Routing answered and parked the caller but produced NO durable interaction (the tenant is quiescing or no
        // service address is configured). The offer must terminate the caller rather than leave it in silence, and it
        // must NEVER promote the leg to Connected — a false-healthy leg would hide an unrouted call from the reconciler.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls);
        var ariClient = new TestAriClient(calls);
        var sink = new TestInboundVoiceEventSink(calls)
        {
            OutcomeInteractionId = null,
        };
        var bridge = CreateBridge(bindingStore, ariClient, sink);
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "channel-1";

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.DoesNotContain("promote-offering:channel-1", calls);
        Assert.Contains("hangup:channel-1", calls);
        Assert.Contains("destroy-bridge:" + holdingBridgeId, calls);
        Assert.Equal("channel-1", bindingStore.RemovedChannelId);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.Equal(AsteriskChannelBindingState.Offering, bindingStore.CreatedBinding.State);
    }

    [Fact]
    public async Task TryHandleAsync_WhenRoutingThrowsAfterParking_RetainsOfferForReconciliationWithoutHangup()
    {
        // Arrange
        // Routing can durably commit the interaction and its queue item and THEN throw (for example, a post-commit
        // publish failure). The offer must NOT hang up the parked caller or delete the Offering binding — doing so
        // would orphan a committed interaction the reconciler could no longer recover. The caller stays parked and the
        // binding stays Offering so the reconciler promotes it (active interaction) or ages it out.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls);
        var ariClient = new TestAriClient(calls);
        var sink = new TestInboundVoiceEventSink(calls)
        {
            RouteException = new InvalidOperationException("post-commit publish failed"),
        };
        var bridge = CreateBridge(bindingStore, ariClient, sink);
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "channel-1";

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Contains("route:call-1", calls);
        Assert.DoesNotContain("hangup:channel-1", calls);
        Assert.DoesNotContain("destroy-bridge:" + holdingBridgeId, calls);
        Assert.DoesNotContain("promote-offering:channel-1", calls);
        Assert.Null(bindingStore.RemovedChannelId);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.Equal(AsteriskChannelBindingState.Offering, bindingStore.CreatedBinding.State);
    }

    [Fact]
    public async Task TryHandleAsync_WhenProvisioningFailsWithAmbiguousTransportError_RetainsBindingForReconciler()
    {
        // Arrange
        // Creating the holding bridge fails with a transport-ambiguous ARI error (no server status code): Asterisk may
        // still create the bridge after this sweep. The compensating hang-up and destroy "succeed" only because the
        // resources are not there yet, which does NOT prove they are absent, so the durable Offering binding must be
        // RETAINED for the age-gated reconciler rather than deleted.
        var bindingStore = new TestBindingStore();
        var ariClient = new TestAriClient
        {
            CreateBridgeException = new AsteriskAriException("createBridge", null, "transport failure"),
        };
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Null(bindingStore.RemovedChannelId);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenProvisioningFailsWithDefiniteClientError_CompensatesAndRemovesBinding()
    {
        // Arrange
        // Creating the holding bridge fails with a definite client-rejection status (4xx): Asterisk did not create the
        // bridge, so compensation is conclusive and the durable Offering binding can be removed after cleanup.
        var bindingStore = new TestBindingStore();
        var ariClient = new TestAriClient
        {
            CreateBridgeException = new AsteriskAriException("createBridge", HttpStatusCode.BadRequest, "rejected"),
        };
        var sink = new TestInboundVoiceEventSink();
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal("channel-1", bindingStore.RemovedChannelId);
        Assert.Null(sink.RoutedEvent);
    }

    private static AsteriskInboundCallOfferBridge CreateBridge(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        IInboundVoiceEventSink sink)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskInboundCallOfferBridge(
            bindingStore,
            ariClient,
            sink,
            clock.Object,
            NullLogger<AsteriskInboundCallOfferBridge>.Instance);
    }

    private static AsteriskRealtimeVoiceEvent CreateInboundEvent()
    {
        return new AsteriskRealtimeVoiceEvent
        {
            IsInbound = true,
            ChannelId = "channel-1",
            CallId = "call-1",
            ProviderName = "Asterisk",
            EventType = "StasisStart",
            CallerNumber = "+15550001000",
            DialedNumber = "+15551234567",
            InteractionCorrelationId = "interaction-1",
            OccurredUtc = _now,
        };
    }

    private sealed class TestBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<string> _calls;

        public TestBindingStore(List<string> calls = null)
        {
            _calls = calls;
        }

        public AsteriskChannelTenantBinding ExistingBinding { get; set; }

        public AsteriskChannelTenantBinding CreatedBinding { get; private set; }

        public string RemovedChannelId { get; private set; }

        public int FindCount { get; private set; }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>([]);
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingBinding is not null);
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
        {
            FindCount++;
            _calls?.Add("find:" + channelId);

            return Task.FromResult(ExistingBinding);
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>([]);
        }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            CreatedBinding = binding;
            _calls?.Add("create-binding:" + binding.ChannelId);

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            RemovedChannelId = channelId;
            _calls?.Add("remove-binding:" + channelId);

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            _calls?.Add("mark-connected:" + channelId);

            return Task.FromResult(true);
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            _calls?.Add("promote-offering:" + channelId);

            if (CreatedBinding is not null &&
                CreatedBinding.ChannelId == channelId &&
                CreatedBinding.State == AsteriskChannelBindingState.Offering)
            {
                CreatedBinding.State = AsteriskChannelBindingState.Connected;

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            _calls?.Add("mark-caller-detached:" + channelId);

            return Task.FromResult(true);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            _calls?.Add("begin-teardown:" + channelId);

            if (ExistingBinding is null
                || ExistingBinding.ChannelId != channelId
                || ExistingBinding.State == AsteriskChannelBindingState.Terminating)
            {
                return Task.FromResult<AsteriskChannelTeardownClaim>(null);
            }

            var previousState = ExistingBinding.State;
            ExistingBinding.State = AsteriskChannelBindingState.Terminating;

            return Task.FromResult(new AsteriskChannelTeardownClaim
            {
                Binding = ExistingBinding,
                PreviousState = previousState,
            });
        }
    }

    private sealed class TestAriClient : IAsteriskAriClient
    {
        private readonly List<string> _calls;

        public TestAriClient(List<string> calls = null)
        {
            _calls = calls ?? [];
        }

        public IReadOnlyList<string> Calls => _calls;

        public bool ThrowOnAnswer { get; set; }

        public bool ThrowOnHangup { get; set; }

        public Exception CreateBridgeException { get; set; }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            _calls.Add($"create-bridge:{bridgeId}:{bridgeType}");

            if (CreateBridgeException is not null)
            {
                throw CreateBridgeException;
            }

            return Task.FromResult(new AsteriskAriBridge
            {
                Id = bridgeId,
            });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            _calls.Add($"add-channel:{bridgeId}:{channelId}");

            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            _calls.Add("answer:" + channelId);

            if (ThrowOnAnswer)
            {
                throw new InvalidOperationException("answer failed");
            }

            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            _calls.Add("hangup:" + channelId);

            if (ThrowOnHangup)
            {
                throw new InvalidOperationException("hangup failed");
            }

            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            _calls.Add("destroy-bridge:" + bridgeId);

            return Task.CompletedTask;
        }
    }

    private sealed class TestInboundVoiceEventSink : IInboundVoiceEventSink
    {
        private readonly List<string> _calls;

        public TestInboundVoiceEventSink(List<string> calls = null)
        {
            _calls = calls;
        }

        public InboundVoiceEvent RoutedEvent { get; private set; }

        public string OutcomeInteractionId { get; set; } = "interaction-1";

        public Exception RouteException { get; set; }

        public Task<InboundVoiceRouteOutcome> RouteAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
        {
            RoutedEvent = inboundEvent;
            _calls?.Add("route:" + inboundEvent.ProviderCallId);

            if (RouteException is not null)
            {
                throw RouteException;
            }

            return Task.FromResult(new InboundVoiceRouteOutcome
            {
                InteractionId = OutcomeInteractionId,
            });
        }
    }
}
