using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskInboundReconcilerTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReconcileAsync_WhenMatchingChannelIsDead_IngestsEndedEventAndRemovesBinding()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "dead-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "call-1",
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = [],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("asterisk", TestContext.Current.CancellationToken);

        // Assert
        var voiceEvent = Assert.Single(sink.IngestedEvents);
        Assert.Equal("Asterisk", voiceEvent.ProviderName);
        Assert.Equal("call-1", voiceEvent.ProviderCallId);
        Assert.Equal(ContactCenterCallState.Ended, voiceEvent.State);
        Assert.Equal(_now, voiceEvent.OccurredUtc);
        Assert.Equal("asterisk-reconcile-hangup-call-1", voiceEvent.IdempotencyKey);
        Assert.Equal("dead-channel-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenMatchingChannelIsAlive_LeavesBindingUntouched()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "alive-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "call-1",
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["alive-channel-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenBindingBelongsToOtherProvider_LeavesBindingUntouched()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "other-channel-1",
                ProviderName = "Other",
                ProviderCallId = "call-1",
            },
        ]);
        var ariClient = new TestAriClient();
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.CheckedChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenBindingProviderNameDoesNotMatch_IsNotReconciled()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "foreign-leg-1",
                ProviderName = "Other Provider",
                ProviderCallId = "call-1",
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = [],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenBindingIsTerminating_CompletesCleanupWithoutCheckingLiveness()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "terminating-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "call-1",
                State = AsteriskChannelBindingState.Terminating,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["terminating-channel-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.CheckedChannelIds);
        var voiceEvent = Assert.Single(sink.IngestedEvents);
        Assert.Equal(ContactCenterCallState.Ended, voiceEvent.State);
        Assert.Equal("asterisk-reconcile-hangup-call-1", voiceEvent.IdempotencyKey);
        Assert.Equal("terminating-channel-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenPendingAgentLegIsRecentAndAlive_LeavesBindingUntouched()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
        Assert.Empty(ariClient.DestroyedBridgeIds);
        Assert.Empty(ariClient.HungupChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenPendingAgentLegIsAgedAndAlive_ReclaimsWithoutEndingCaller()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Equal("agent-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenAgedPendingLegHasDetachedCallerStillAlive_ReturnsCallerToHoldingBeforeRetiring()
    {
        // Arrange
        // Model a connect that durably marked the caller detached from holding then crashed before finalizing. The
        // aged pending agent leg is reclaimed, and because the caller is still alive with no bridge it must be
        // re-parked into a fresh holding bridge for re-offer BEFORE the record is retired — never left in silence.
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CallerDetached = true,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1", "caller-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.Contains(ariClient.CreatedBridges, bridge =>
            bridge.BridgeId == holdingBridgeId &&
            bridge.BridgeType == AsteriskAriConstants.HoldingBridgeType);
        Assert.Contains(ariClient.AddedChannels, call =>
            call.BridgeId == holdingBridgeId && call.ChannelId == "caller-1");
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Equal("agent-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenDetachedCallerHasAlreadyHungUp_RetiresRecordWithoutReParking()
    {
        // Arrange
        // The caller marked detached has since hung up on its own, so there is nothing to re-park. The record is
        // retired without creating a holding bridge.
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CallerDetached = true,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.DoesNotContain(ariClient.CreatedBridges, bridge => bridge.BridgeId == holdingBridgeId);
        Assert.Equal("agent-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenReParkingDetachedCallerFails_RetainsRecordForRetry()
    {
        // Arrange
        // A transient ARI failure while re-parking the still-alive detached caller must NOT retire the durable
        // record; leaving it Terminating lets a later sweep retry the re-park instead of stranding the caller.
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CallerDetached = true,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1", "caller-1"],
            CreateBridgeShouldThrow = (bridgeId, _) => bridgeId == holdingBridgeId,
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenConnectedAgentLegIsDead_ReleasesCallerPeerAndEndsCall()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
            },
            new AsteriskChannelTenantBinding
            {
                ChannelId = "caller-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = [],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("crestapps-holding-caller-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.Contains("caller-1", ariClient.HungupChannelIds);

        var voiceEvent = Assert.Single(sink.IngestedEvents);
        Assert.Equal("caller-1", voiceEvent.ProviderCallId);
        Assert.Equal("asterisk-reconcile-hangup-caller-1", voiceEvent.IdempotencyKey);
        Assert.Contains("agent-1", bindingStore.RemovedChannelIds);
        Assert.Contains("caller-1", bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenCallerLegIsDeadWithMultiplePeerGenerations_TearsDownEveryGeneration()
    {
        // Arrange
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "caller-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
            },
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-old",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-old",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
            },
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-new",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-new",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-old", "agent-new"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-old", ariClient.DestroyedBridgeIds);
        Assert.Contains("bridge-new", ariClient.DestroyedBridgeIds);
        Assert.Contains("crestapps-holding-caller-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-old", ariClient.HungupChannelIds);
        Assert.Contains("agent-new", ariClient.HungupChannelIds);
        Assert.Contains("caller-1", ariClient.HungupChannelIds);

        var voiceEvent = Assert.Single(sink.IngestedEvents);
        Assert.Equal("asterisk-reconcile-hangup-caller-1", voiceEvent.IdempotencyKey);
        Assert.Contains("agent-old", bindingStore.RemovedChannelIds);
        Assert.Contains("agent-new", bindingStore.RemovedChannelIds);
        Assert.Contains("caller-1", bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenRetainedProvisioningRecordHasNotAged_ReClaimsResourcesButKeepsRecord()
    {
        // Arrange
        // A live terminal event claimed a still-provisioning (Pending) agent leg and deliberately left the durable
        // record Terminating instead of deleting it, because the connect allocator had not yet quiesced. Before the
        // provisioning lease elapses the reconciler must idempotently re-run the agent-leg cleanup (so a resource the
        // allocator created a moment after the claim is still swept) but must NOT retire the record yet, and must
        // never emit an ended projection for a leg that was never a live call.
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenRetainedProvisioningRecordHasAged_ReClaimsResourcesAndRetiresRecord()
    {
        // Arrange
        // Once the provisioning lease has elapsed the connect allocator is provably gone, so the retained Terminating
        // record is cleaned a final time and retired. A Pending disposition still never emits an ended projection.
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "bridge-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["agent-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("bridge-1", ariClient.DestroyedBridgeIds);
        Assert.Contains("agent-1", ariClient.HungupChannelIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Equal("agent-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenOfferingCallerLegIsRecent_LeavesBindingUntouched()
    {
        // Arrange
        // An inbound caller leg still in the Offering state is owned by the in-flight offer flow. Until its lease
        // elapses the reconciler must never touch it, regardless of channel liveness, because the offer may still be
        // parking or routing the caller.
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "caller-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                State = AsteriskChannelBindingState.Offering,
                CreatedUtc = _now,
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.DestroyedBridgeIds);
        Assert.Empty(ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAgedOfferingCallerLegIsAlive_TerminatesHoldingBridgeAndCallerWithoutEndingCall()
    {
        // Arrange
        // The offer flow crashed after answering and parking the caller but before routing it to an interaction, so
        // the leg is stuck Offering. Once aged it is reclaimed: the caller's derived holding bridge is destroyed and
        // the still-parked caller is gracefully hung up (there is no interaction to resume). Crucially, an unrouted
        // offer never became a live call, so NO ended projection is emitted.
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "caller-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                State = AsteriskChannelBindingState.Offering,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(holdingBridgeId, ariClient.DestroyedBridgeIds);
        Assert.Contains("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Equal("caller-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenAgedOfferingCallerLegIsDead_DestroysHoldingBridgeAndRetiresWithoutHangup()
    {
        // Arrange
        // The unrouted Offering caller has already hung up on its own, so only the derived holding bridge needs to be
        // torn down before the record is retired. No hangup and no ended projection are produced.
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var bindingStore = new TestBindingStore(
        [
            new AsteriskChannelTenantBinding
            {
                ChannelId = "caller-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                State = AsteriskChannelBindingState.Offering,
                CreatedUtc = _now.AddMinutes(-10),
            },
        ]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = [],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink);

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(holdingBridgeId, ariClient.DestroyedBridgeIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Equal("caller-1", Assert.Single(bindingStore.RemovedChannelIds));
    }

    [Fact]
    public async Task ReconcileAsync_WhenAgedOfferingCallerLegHasActiveInteraction_PromotesInsteadOfTerminating()
    {
        // Arrange
        // The offer flow routed the caller to a durable interaction but crashed before its deferred Offering->Connected
        // promote ran, so the leg is stuck Offering with a live, routed caller. Once aged, the reconciler must recover it
        // FORWARD by promoting it to Connected — never tear down a live call. No holding bridge is destroyed, the caller
        // is never hung up, no ended projection is emitted, and the durable record is retained (now Connected).
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";
        var binding = new AsteriskChannelTenantBinding
        {
            ChannelId = "caller-1",
            ProviderName = "Asterisk",
            ProviderCallId = "caller-1",
            State = AsteriskChannelBindingState.Offering,
            CreatedUtc = _now.AddMinutes(-10),
        };
        var bindingStore = new TestBindingStore([binding]);
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
        };
        var sink = new TestProviderVoiceEventSink();
        var reconciler = CreateReconciler(bindingStore, ariClient, sink, new TestInteractionProbe(hasActiveInteraction: true));

        // Act
        await reconciler.ReconcileAsync("Asterisk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AsteriskChannelBindingState.Connected, binding.State);
        Assert.DoesNotContain(holdingBridgeId, ariClient.DestroyedBridgeIds);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannelIds);
        Assert.Empty(sink.IngestedEvents);
        Assert.Empty(bindingStore.RemovedChannelIds);
    }

    private static AsteriskInboundReconciler CreateReconciler(
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskAriClient ariClient,
        IProviderVoiceEventSink sink,
        IInboundVoiceInteractionProbe interactionProbe = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskInboundReconciler(
            bindingStore,
            ariClient,
            sink,
            interactionProbe ?? new TestInteractionProbe(),
            clock.Object,
            NullLogger<AsteriskInboundReconciler>.Instance);
    }

    private sealed class TestInteractionProbe : IInboundVoiceInteractionProbe
    {
        private readonly bool _hasActiveInteraction;

        public TestInteractionProbe(bool hasActiveInteraction = false)
        {
            _hasActiveInteraction = hasActiveInteraction;
        }

        public Task<bool> HasActiveInteractionAsync(
            string providerName,
            string providerCallId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_hasActiveInteraction);
        }
    }

    private sealed class TestBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings;

        public TestBindingStore(IEnumerable<AsteriskChannelTenantBinding> bindings)
        {
            _bindings = bindings.ToList();
        }

        public List<string> RemovedChannelIds { get; } = [];

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            // Return a cloned snapshot so mutations the sweep applies to the store's bindings (a claim flipping a
            // binding to Terminating, or a removal) do not retroactively change the objects the reconciler is
            // iterating, exactly as a fresh YesSql query would.
            IReadOnlyCollection<AsteriskChannelTenantBinding> snapshot = _bindings.Select(Clone).ToArray();

            return Task.FromResult(snapshot);
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bindings.Count > 0);
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
        {
            IReadOnlyCollection<AsteriskChannelTenantBinding> matches = _bindings
                .Where(item => item.PeerChannelId == peerChannelId)
                .Select(Clone)
                .ToArray();

            return Task.FromResult(matches);
        }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            throw new NotSupportedException();
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            RemovedChannelIds.Add(channelId);

            var existing = _bindings.FirstOrDefault(item => item.ChannelId == channelId);

            if (existing is not null)
            {
                _bindings.Remove(existing);
            }

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            return Task.FromResult(true);
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            var binding = _bindings.FirstOrDefault(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Offering)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Connected;

            return Task.FromResult(true);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            var binding = _bindings.FirstOrDefault(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
            {
                return Task.FromResult(false);
            }

            binding.CallerDetached = true;

            return Task.FromResult(true);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            var binding = _bindings.FirstOrDefault(item => item.ChannelId == channelId);

            if (binding is null || binding.State == AsteriskChannelBindingState.Terminating)
            {
                return Task.FromResult<AsteriskChannelTeardownClaim>(null);
            }

            var previousState = binding.State;
            binding.State = AsteriskChannelBindingState.Terminating;
            binding.PreTeardownState = previousState;

            return Task.FromResult(new AsteriskChannelTeardownClaim
            {
                Binding = binding,
                PreviousState = previousState,
            });
        }

        private static AsteriskChannelTenantBinding Clone(AsteriskChannelTenantBinding binding)
        {
            return new AsteriskChannelTenantBinding
            {
                ChannelId = binding.ChannelId,
                ProviderName = binding.ProviderName,
                InteractionId = binding.InteractionId,
                ProviderCallId = binding.ProviderCallId,
                BridgeId = binding.BridgeId,
                PeerChannelId = binding.PeerChannelId,
                State = binding.State,
                PreTeardownState = binding.PreTeardownState,
                CallerDetached = binding.CallerDetached,
                CreatedUtc = binding.CreatedUtc,
            };
        }
    }

    private sealed class TestAriClient : IAsteriskAriClient
    {
        public HashSet<string> ExistingChannels { get; set; } = [];

        public List<string> CheckedChannelIds { get; } = [];

        public List<(string BridgeId, string BridgeType)> CreatedBridges { get; } = [];

        public List<(string BridgeId, string ChannelId)> AddedChannels { get; } = [];

        public Func<string, string, bool> CreateBridgeShouldThrow { get; set; }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            CreatedBridges.Add((bridgeId, bridgeType));

            if (CreateBridgeShouldThrow?.Invoke(bridgeId, bridgeType) == true)
            {
                throw new InvalidOperationException("Simulated createBridge failure.");
            }

            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            AddedChannels.Add((bridgeId, channelId));

            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public List<string> DestroyedBridgeIds { get; } = [];

        public List<string> HungupChannelIds { get; } = [];

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannelIds.Add(channelId);

            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            CheckedChannelIds.Add(channelId);

            return Task.FromResult(ExistingChannels.Contains(channelId));
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            DestroyedBridgeIds.Add(bridgeId);

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

        public Task<AsteriskAriChannel> SnoopChannelAsync(string channelId, string spy, string whisper, string snoopId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel { Id = snoopId });
        }
    }

    private sealed class TestProviderVoiceEventSink : IProviderVoiceEventSink
    {
        public List<ProviderVoiceEvent> IngestedEvents { get; } = [];

        public Task<bool> IngestAsync(ProviderVoiceEvent providerEvent, CancellationToken cancellationToken = default)
        {
            IngestedEvents.Add(providerEvent);

            return Task.FromResult(true);
        }
    }
}
