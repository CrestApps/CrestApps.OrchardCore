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
    public async Task TryHandleAsync_WhenCreateLockTimesOut_HangsUpCallerAndAbsorbsEventWithoutStranding()
    {
        // Arrange
        // The per-channel create-serialization lock could not be acquired within its bounded window, so CreateAsync
        // throws and no binding is persisted. The reconciler sweeps only existing bindings, so a bare rethrow would
        // strand the live inbound caller unanswered and untracked forever. The offer bridge must fail safe: hang the
        // caller up deterministically and absorb the event so the dispatcher does not fall through to ingestion.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls)
        {
            ThrowCreateTimeout = true,
        };
        var ariClient = new TestAriClient(calls);
        var sink = new TestInboundVoiceEventSink(calls);
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(
        [
            "find:channel-1",
            "create-binding-timeout:channel-1",
            "claim:channel-1",
            "hangup:channel-1",
            "release:channel-1",
        ], calls);
        Assert.Null(bindingStore.CreatedBinding);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenCreateLockTimesOutButAnotherDeliveryRecoveredTheCaller_DoesNotHangUpTheLiveCall()
    {
        // Arrange
        // A different delivery acquired the create lock after this one timed out and legitimately claimed, answered,
        // and routed the same caller, so a binding now exists for the channel. The fail-safe must never hang up a
        // recovered, live call: it checks for a binding before terminating and neither hangs the caller up nor
        // enqueues it for the reconciliation sweep.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls)
        {
            ThrowCreateTimeout = true,
            SimulateRecoveryAfterCreateTimeout = true,
        };
        var ariClient = new TestAriClient(calls);
        var sink = new TestInboundVoiceEventSink(calls);
        var registry = new TestPendingCallerTerminationRegistry();
        var bridge = CreateBridge(bindingStore, ariClient, sink, registry);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.DoesNotContain("hangup:channel-1", calls);
        Assert.Empty(registry.EnqueuedChannelIds);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenCreateLockTimesOutAndHangupFailsTransiently_RetriesUntilTheCallerIsTerminated()
    {
        // Arrange
        // No binding is persisted on a create-lock timeout, so the binding-scoped reconciler cannot retry the caller
        // termination. A single transient ARI failure must therefore not strand the live caller: the offer bridge
        // retries the hang up within a bounded budget, converting a momentary blip into a successful termination.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls)
        {
            ThrowCreateTimeout = true,
        };
        var ariClient = new TestAriClient(calls)
        {
            HangupFailuresBeforeSuccess = 2,
        };
        var sink = new TestInboundVoiceEventSink(calls);
        var bridge = CreateBridge(bindingStore, ariClient, sink);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(3, calls.Count(call => call == "hangup:channel-1"));
        Assert.Null(bindingStore.CreatedBinding);
        Assert.Null(sink.RoutedEvent);
    }

    [Fact]
    public async Task TryHandleAsync_WhenCreateLockTimesOutAndHangupNeverSucceeds_AbsorbsEventWithoutOrphanAfterBoundedRetries()
    {
        // Arrange
        // When every bounded hang-up attempt fails the event is still absorbed (the dispatcher must not fall through
        // to ingestion) and nothing is orphaned; the persistent failure is escalated to an error elsewhere so the
        // residual live caller is operator-visible for out-of-band reconciliation.
        var calls = new List<string>();
        var bindingStore = new TestBindingStore(calls)
        {
            ThrowCreateTimeout = true,
        };
        var ariClient = new TestAriClient(calls)
        {
            ThrowOnHangup = true,
        };
        var sink = new TestInboundVoiceEventSink(calls);
        var registry = new TestPendingCallerTerminationRegistry();
        var bridge = CreateBridge(bindingStore, ariClient, sink, registry);

        // Act
        var handled = await bridge.TryHandleAsync(CreateInboundEvent(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(3, calls.Count(call => call == "hangup:channel-1"));
        Assert.Null(bindingStore.CreatedBinding);
        Assert.Null(sink.RoutedEvent);
        Assert.Contains("channel-1", registry.EnqueuedChannelIds);
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
        IInboundVoiceEventSink sink,
        IAsteriskPendingCallerTerminationRegistry pendingCallerTerminationRegistry = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskInboundCallOfferBridge(
            bindingStore,
            ariClient,
            sink,
            pendingCallerTerminationRegistry ?? new TestPendingCallerTerminationRegistry(),
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

        public bool ThrowCreateTimeout { get; set; }

        public bool SimulateRecoveryAfterCreateTimeout { get; set; }

        private bool _createTimedOut;

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>([]);
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingBinding is not null);
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId, CancellationToken cancellationToken = default)
        {
            FindCount++;
            _calls?.Add("find:" + channelId);

            return Task.FromResult(ExistingBinding);
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>([]);
        }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            if (ThrowCreateTimeout)
            {
                _calls?.Add("create-binding-timeout:" + binding.ChannelId);
                _createTimedOut = true;

                throw new AsteriskChannelBindingCreateTimeoutException(binding.ChannelId, TimeSpan.FromSeconds(10));
            }

            CreatedBinding = binding;
            _calls?.Add("create-binding:" + binding.ChannelId);

            return Task.FromResult(true);
        }

        public Task<bool> TryClaimChannelForTerminationAsync(string channelId, CancellationToken cancellationToken = default)
        {
            _calls?.Add("claim:" + channelId);

            if (SimulateRecoveryAfterCreateTimeout && _createTimedOut)
            {
                // Model a different delivery that acquired the freed create lock after this one timed out and
                // legitimately claimed, answered, and routed the same caller: a binding now exists, so the claim is
                // refused and the recovered, live call must not be hung up.
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public void ReleaseTerminationClaim(string channelId)
        {
            _calls?.Add("release:" + channelId);
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

        public Task<bool> SwapConnectedOwnerAsync(string newAgentChannelId, string previousAgentChannelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TryPromoteJoiningToParticipatingAsync(string channelId)
        {
            _calls?.Add("promote-joining-participating:" + channelId);

            return Task.FromResult(false);
        }

        public Task<bool> TryHandOffBridgeOwnershipAsync(string bridgeId, string departingOwnerChannelId)
        {
            _calls?.Add("handoff-bridge:" + bridgeId);

            return Task.FromResult(false);
        }

        public Task<bool> TryClaimProvisionalLegForTeardownAsync(string channelId)
        {
            _calls?.Add("claim-provisional-teardown:" + channelId);

            return Task.FromResult(false);
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

        public Task<bool> PromoteParticipantToConnectedOwnerAsync(string participantChannelId, string previousAgentChannelId)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class TestPendingCallerTerminationRegistry : IAsteriskPendingCallerTerminationRegistry
    {
        private readonly HashSet<string> _claims = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> EnqueuedChannelIds => _pending.ToArray();

        public bool HasTerminationClaim(string channelId)
        {
            return _claims.Contains(channelId);
        }

        public void PlantTerminationClaim(string channelId)
        {
            _claims.Add(channelId);
        }

        public void RemoveTerminationClaim(string channelId)
        {
            _claims.Remove(channelId);
        }

        public void Enqueue(string channelId)
        {
            _pending.Add(channelId);
        }

        public void Resolve(string channelId)
        {
            _pending.Remove(channelId);
        }

        public IReadOnlyCollection<string> GetPending()
        {
            return _pending.ToArray();
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

        public int HangupFailuresBeforeSuccess { get; set; }

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

            if (HangupFailuresBeforeSuccess > 0)
            {
                HangupFailuresBeforeSuccess--;

                throw new InvalidOperationException("hangup failed transiently");
            }

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

        public Task<AsteriskAriLiveRecording> StartBridgeRecordingAsync(string bridgeId, string recordingName, string format, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriLiveRecording
            {
                Name = recordingName,
                Format = format,
                State = "recording",
            });
        }

        public Task PauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UnpauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<AsteriskAriStoredRecording> StopBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            return Task.FromResult<AsteriskAriStoredRecording>(null);
        }

        public Task<AsteriskAriStoredRecordingContent> DownloadStoredRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            return Task.FromResult<AsteriskAriStoredRecordingContent>(null);
        }

        public Task DeleteStoredRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<AsteriskAriChannel> SnoopChannelAsync(string channelId, string spy, string whisper, string snoopId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel { Id = snoopId });
        }

        public Task HoldChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UnholdChannelAsync(string channelId, CancellationToken cancellationToken)
        {
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
