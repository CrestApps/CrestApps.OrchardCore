using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceProviderTransferTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _callerChannelId = "caller-1";
    private const string _interactionId = "interaction-1";
    private const string _targetUserId = "agent-2";
    private const string _currentAgentChannelId = "crestapps-agent-interaction-1-command-1";
    private static readonly string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-command-1";
    private static readonly string _newAgentChannelId = AsteriskAriConstants.TransferAgentChannelPrefix + "interaction-1-agent-2";

    [Fact]
    public async Task TransferAsync_WhenBlindToAgent_OriginatesNewLegAddsToBridgeHangsUpOldLegAndSwapsBinding()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);

        Assert.Equal(_newAgentChannelId, ariClient.OriginatedChannelId);
        Assert.Equal("PJSIP/agent2-endpoint", ariClient.OriginatedEndpoint);
        Assert.Contains((_mixingBridgeId, _newAgentChannelId), ariClient.AddedToBridge);
        Assert.Contains(_currentAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_callerChannelId, ariClient.HungupChannels);

        var newBinding = bindingStore.Find(_newAgentChannelId);
        Assert.NotNull(newBinding);
        Assert.Equal(AsteriskChannelBindingState.Connected, newBinding.State);
        Assert.Equal(_mixingBridgeId, newBinding.BridgeId);
        Assert.Equal(_callerChannelId, newBinding.PeerChannelId);
        Assert.Null(bindingStore.Find(_currentAgentChannelId));

        // The destination leg's durable Joining claim is persisted BEFORE the leg is originated, so a concurrent
        // duplicate transfer loses the serialized create and can never re-ring the destination.
        var claimIndex = ariClient.Operations.IndexOf($"createBinding:{_newAgentChannelId}");
        var originateIndex = ariClient.Operations.IndexOf($"originate:{_newAgentChannelId}");
        Assert.True(claimIndex >= 0 && originateIndex >= 0);
        Assert.True(claimIndex < originateIndex, "The durable transfer claim must be persisted before the destination leg is originated.");

        // Ownership transfers with an atomic swap (promote destination + retire previous in one transaction) only
        // AFTER the destination joins the canonical bridge, and the previous leg is hung up only AFTER the swap, so
        // the bridge is owned by exactly one Connected binding at every instant.
        var addIndex = ariClient.Operations.IndexOf($"add:{_mixingBridgeId}:{_newAgentChannelId}");
        var swapIndex = ariClient.Operations.IndexOf($"swap:{_newAgentChannelId}:{_currentAgentChannelId}");
        var hangupOldIndex = ariClient.Operations.IndexOf($"hangup:{_currentAgentChannelId}");
        Assert.True(addIndex >= 0 && swapIndex >= 0 && hangupOldIndex >= 0);
        Assert.True(addIndex < swapIndex, "The destination leg must join the canonical bridge before ownership is swapped.");
        Assert.True(swapIndex < hangupOldIndex, "The previous leg must be hung up only after ownership is atomically swapped.");

        Assert.Equal(_newAgentChannelId, result.Metadata[ContactCenterConstants.TransferMetadata.NewChannelId]);
        Assert.Equal(_mixingBridgeId, result.Metadata[ContactCenterConstants.TransferMetadata.BridgeId]);
    }

    [Fact]
    public async Task TransferAsync_WhenNewLegIsAddedToBridgeBeforeOldLegIsHungUp_PreservesRecordingContinuity()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations), CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // The new agent leg must be committed to the canonical bridge before the previous leg is hung up, so the
        // customer never leaves the recorded conversation bridge during the swap.
        var addIndex = ariClient.Operations.IndexOf($"add:{_mixingBridgeId}:{_newAgentChannelId}");
        var hangupIndex = ariClient.Operations.IndexOf($"hangup:{_currentAgentChannelId}");
        Assert.True(addIndex >= 0 && hangupIndex >= 0);
        Assert.True(addIndex < hangupIndex, "The destination leg must join the canonical bridge before the old leg is hung up.");

        // No new bridge is created; the customer stays on the same canonical mixing bridge.
        Assert.Empty(ariClient.CreatedBridges);
    }

    [Fact]
    public async Task TransferAsync_WhenDestinationDoesNotAnswer_HangsUpNewLegAndLeavesOriginalIntact()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId);
        var service = CreateService(
            ariClient,
            bindingStore,
            CreateAgentLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(ready: false));

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("transfer_no_answer", result.ErrorCode);

        Assert.Contains(_newAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_currentAgentChannelId, ariClient.HungupChannels);

        // The original agent leg binding is untouched and no new agent-leg binding is left behind.
        Assert.NotNull(bindingStore.Find(_currentAgentChannelId));
        Assert.Null(bindingStore.Find(_newAgentChannelId));
    }

    [Fact]
    public async Task TransferAsync_WhenNoOwningBindingExists_FailsClosed()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(ariClient, new TestTransferBindingStore(), CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("transfer_call_not_owned", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenConsultative_ReturnsConfirmedUnsupportedFailure()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(_currentAgentChannelId), CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(transferType: InteractionTransferType.Consultative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("transfer_type_unsupported", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenTargetIsNotAgent_ReturnsConfirmedUnsupportedFailure()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(_currentAgentChannelId), CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(targetType: InteractionTransferTargetType.Queue),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("transfer_target_unsupported", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenDestinationAgentMetadataMissing_ReturnsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(_currentAgentChannelId), CreateAgentLeaseStore());

        var request = CreateRequest();
        request.Metadata.Clear();

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("transfer_target_missing", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenDestinationAgentIsOffline_FailsClosed()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var service = CreateService(
            ariClient,
            CreateConnectedBindingStore(_currentAgentChannelId),
            new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("transfer_target_offline", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenTransferAlreadyCompleted_IsIdempotentSuccessWithoutReoriginating()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();

        // The owned agent leg IS the deterministic destination leg, so the transfer already completed; a retry must
        // confirm the completed transfer rather than ring the destination a second time.
        var bindingStore = CreateConnectedBindingStore(_newAgentChannelId);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Empty(ariClient.HungupChannels);
        Assert.Equal(_newAgentChannelId, result.Metadata[ContactCenterConstants.TransferMetadata.NewChannelId]);
    }

    [Fact]
    public async Task TransferAsync_WhenAriTimesOutAmbiguously_ReportsOutcomeUnknownAndCompensatesNewLeg()
    {
        // Arrange
        var ariClient = new TestTransferAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                statusCode: null,
                "Asterisk ARI timed out before a response was observed.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),
        };
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("transfer_outcome_unknown", result.ErrorCode);

        // The original call is left intact; only this transfer's own new leg is compensated.
        Assert.Contains(_newAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_currentAgentChannelId, ariClient.HungupChannels);
        Assert.NotNull(bindingStore.Find(_currentAgentChannelId));
    }

    [Fact]
    public async Task TransferAsync_WhenAriRejects_ReportsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestTransferAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                HttpStatusCode.BadRequest,
                "Asterisk ARI rejected the originate request."),
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(_currentAgentChannelId), CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("transfer_failed", result.ErrorCode);
    }

    [Fact]
    public async Task TransferAsync_WhenCancelledDuringAddToBridge_CompensatesNewLegAndRethrows()
    {
        // Arrange
        var ariClient = new TestTransferAriClient
        {
            AddChannelToBridgeException = new OperationCanceledException(),
        };
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken));

        // The bridge add is the commit point, so a cancellation there is pre-commit: only this transfer's own new leg
        // is released and the original call stays intact. No new ownership binding is ever written before commit.
        Assert.Contains(_newAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_currentAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_callerChannelId, ariClient.HungupChannels);
        Assert.Null(bindingStore.Find(_newAgentChannelId));
        Assert.NotNull(bindingStore.Find(_currentAgentChannelId));
    }

    [Fact]
    public async Task TransferAsync_WhenCancelledWhileWaitingForAnswer_RethrowsInsteadOfReportingNoAnswer()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId);

        // A cancelled readiness wait surfaces as a not-ready result; the provider must distinguish it from a genuine
        // no-answer and rethrow so the core reports an unknown (not confirmed no-answer) outcome.
        var service = CreateService(
            ariClient,
            bindingStore,
            CreateAgentLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(ready: false));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.TransferAsync(
            CreateRequest(),
            cancellation.Token));

        // The original call is untouched and the transfer is not misreported as a completed no-answer teardown.
        Assert.Contains(_newAgentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_currentAgentChannelId, ariClient.HungupChannels);
        Assert.NotNull(bindingStore.Find(_currentAgentChannelId));
    }

    [Fact]
    public async Task TransferAsync_WhenCallNotOwnedAndDestinationOffline_FailsClosedAsNotOwned()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();

        // Ownership is validated before the destination's registration, so an unowned call always fails closed with
        // the single unambiguous ownership reason and never leaks whether the destination is registered.
        var service = CreateService(ariClient, new TestTransferBindingStore(), new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("transfer_call_not_owned", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task TransferAsync_WhenPreviousLegHangupIsUnconfirmed_StillSwapsOwnershipAtomicallyToTheDestination()
    {
        // Arrange
        var ariClient = new TestTransferAriClient
        {
            HangupExceptionChannelId = _currentAgentChannelId,
        };
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        // The destination leg is already committed to the customer's bridge, so the transfer physically succeeded.
        Assert.True(result.Succeeded);
        Assert.Contains((_mixingBridgeId, _newAgentChannelId), ariClient.AddedToBridge);

        // The atomic swap promotes the destination to the sole Connected owner and retires the previous binding to a
        // non-owning Terminating (Joining-disposition) recovery record in one transaction, so even though the previous
        // leg's hangup could not be confirmed the canonical bridge still has exactly one Connected owner (the
        // destination). The previous leg's unconfirmed hangup no longer leaks: its retired binding survives as a
        // durable recovery record the reconciler reclaims.
        var destination = bindingStore.Find(_newAgentChannelId);
        Assert.NotNull(destination);
        Assert.Equal(AsteriskChannelBindingState.Connected, destination.State);
        Assert.Equal(_mixingBridgeId, destination.BridgeId);

        var previous = bindingStore.Find(_currentAgentChannelId);
        Assert.NotNull(previous);
        Assert.Equal(AsteriskChannelBindingState.Terminating, previous.State);
        Assert.Equal(AsteriskChannelBindingState.Joining, previous.PreTeardownState);
    }

    [Fact]
    public async Task TransferAsync_WhenDestinationLegClaimedForTeardownBeforeSwap_KeepsCustomerWithPreviousAgent()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);

        // Simulate a terminal event claiming the destination leg for teardown between the bridge commit and the swap:
        // the atomic owner-swap then loses the race and does not commit.
        bindingStore.SwapAlwaysFails = true;
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        // The destination physically bridged, so the transfer reports success, but because the swap lost the race the
        // previous agent binding is deliberately retained as the Connected owner so the customer keeps the previous
        // agent — never a drop, never a window with zero owners.
        Assert.True(result.Succeeded);
        Assert.Contains((_mixingBridgeId, _newAgentChannelId), ariClient.AddedToBridge);

        var previous = bindingStore.Find(_currentAgentChannelId);
        Assert.NotNull(previous);
        Assert.Equal(AsteriskChannelBindingState.Connected, previous.State);

        // The previous leg is never hung up because ownership never transferred, and the destination binding is left
        // Joining (non-owning) for the reconciler to reclaim.
        Assert.DoesNotContain(_currentAgentChannelId, ariClient.HungupChannels);
        var destination = bindingStore.Find(_newAgentChannelId);
        Assert.NotNull(destination);
        Assert.Equal(AsteriskChannelBindingState.Joining, destination.State);
    }

    [Fact]
    public async Task TransferAsync_WhenConcurrentDuplicateLostTheDurableClaim_IsIdempotentSuccessWithoutReoriginating()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);

        // A concurrent duplicate transfer to the same destination already persisted the durable Joining claim on the
        // deterministic destination leg and originated it, so this request's serialized create must lose.
        bindingStore.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _newAgentChannelId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _mixingBridgeId,
            State = AsteriskChannelBindingState.Joining,
            CreatedUtc = _now,
        });
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        // Losing the durable claim means the duplicate confirms the in-flight transfer's outcome without re-ringing
        // the destination or disturbing the concurrent transfer's leg.
        Assert.True(result.Succeeded);
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.DoesNotContain(_newAgentChannelId, ariClient.HungupChannels);
        Assert.NotNull(bindingStore.Find(_newAgentChannelId));
        Assert.Equal(_newAgentChannelId, result.Metadata[ContactCenterConstants.TransferMetadata.NewChannelId]);
    }

    [Fact]
    public async Task TransferAsync_WhenPriorClaimIsStaleTerminating_FailsClosedInsteadOfReportingSuccess()
    {
        // Arrange
        var ariClient = new TestTransferAriClient();
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);

        // A prior transfer attempt's destination leg died and left a Terminating recovery record (Joining
        // disposition) the reconciler has not yet retired. This request's serialized create loses because the
        // deterministic destination id still exists, but there is NO live destination leg behind it.
        bindingStore.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _newAgentChannelId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _mixingBridgeId,
            State = AsteriskChannelBindingState.Terminating,
            PreTeardownState = AsteriskChannelBindingState.Joining,
            CreatedUtc = _now,
        });
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        // A stale claim has no live leg, so confirming success would falsely report a completed transfer while the
        // customer still talks to the previous agent. Fail closed (confirmed, retryable) instead, and never re-ring.
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("transfer_failed", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);

        var previous = bindingStore.Find(_currentAgentChannelId);
        Assert.NotNull(previous);
        Assert.Equal(AsteriskChannelBindingState.Connected, previous.State);
    }

    [Fact]
    public async Task TransferAsync_WhenOriginateIsAmbiguousAndHangupUnconfirmed_RetainsRecoveryRecordForReconciler()
    {
        // Arrange
        var ariClient = new TestTransferAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                statusCode: null,
                "Asterisk ARI timed out before a response was observed.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),

            // The compensating hangup for the possibly-live destination leg also times out (unconfirmed).
            HangupExceptionChannelId = _newAgentChannelId,
        };
        var bindingStore = CreateConnectedBindingStore(_currentAgentChannelId, ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateAgentLeaseStore());

        // Act
        var result = await service.TransferAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("transfer_outcome_unknown", result.ErrorCode);

        // The ambiguous originate may have created the destination channel and its hangup was not confirmed, so the
        // durable claim is RETAINED as a Terminating recovery record (Joining disposition) — never hard-deleted — so
        // the age-gated reconciler reclaims the dangling channel instead of leaking it.
        var destination = bindingStore.Find(_newAgentChannelId);
        Assert.NotNull(destination);
        Assert.Equal(AsteriskChannelBindingState.Terminating, destination.State);
        Assert.Equal(AsteriskChannelBindingState.Joining, destination.PreTeardownState);

        // The original call is left intact.
        Assert.NotNull(bindingStore.Find(_currentAgentChannelId));
    }

    private static ContactCenterVoiceTransferRequest CreateRequest(
        InteractionTransferType transferType = InteractionTransferType.Blind,
        InteractionTransferTargetType targetType = InteractionTransferTargetType.Agent)
    {
        return new ContactCenterVoiceTransferRequest
        {
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            TransferType = transferType,
            TargetType = targetType,
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.TransferMetadata.AgentUserId] = _targetUserId,
            },
        };
    }

    private static TestTransferBindingStore CreateConnectedBindingStore(string agentChannelId, List<string> operations = null)
    {
        var store = new TestTransferBindingStore(operations);
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = agentChannelId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _mixingBridgeId,
            State = AsteriskChannelBindingState.Connected,
            CreatedUtc = _now,
        });

        return store;
    }

    private static FakeAsteriskPjsipCredentialLeaseStore CreateAgentLeaseStore()
    {
        return new FakeAsteriskPjsipCredentialLeaseStore(new AsteriskPjsipCredentialLease
        {
            UserId = _targetUserId,
            AuthorizationUser = "agent2-endpoint",
            IssuedUtc = _now,
        });
    }

    private static AsteriskContactCenterVoiceProvider CreateService(
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskPjsipCredentialLeaseStore leaseStore,
        IAsteriskAgentChannelReadySignal readySignal = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore,
            leaseStore,
            readySignal ?? new FakeAsteriskAgentChannelReadySignal(),
            new FakeAsteriskRecordingIngestJobStore(),
            clock.Object,
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            new TestStringLocalizer());
    }

    private sealed class TestStringLocalizer : IStringLocalizer<AsteriskContactCenterVoiceProvider>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }

    private sealed class TestTransferAriClient : IAsteriskAriClient
    {
        public Exception OriginateException { get; set; }

        public Exception AddChannelToBridgeException { get; set; }

        public string HangupExceptionChannelId { get; set; }

        public string OriginatedChannelId { get; private set; }

        public string OriginatedEndpoint { get; private set; }

        public List<(string BridgeId, string ChannelId)> AddedToBridge { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public List<string> CreatedBridges { get; } = [];

        public List<string> Operations { get; } = [];

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            if (OriginateException is not null)
            {
                return Task.FromException<AsteriskAriChannel>(OriginateException);
            }

            OriginatedChannelId = request.ChannelId;
            OriginatedEndpoint = request.Endpoint;
            Operations.Add($"originate:{request.ChannelId}");

            return Task.FromResult(new AsteriskAriChannel { Id = request.ChannelId });
        }

        public Task<AsteriskAriChannel> SnoopChannelAsync(string channelId, string spy, string whisper, string snoopId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel { Id = snoopId });
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            CreatedBridges.Add(bridgeId);
            Operations.Add($"createBridge:{bridgeId}");

            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            if (AddChannelToBridgeException is not null)
            {
                return Task.FromException(AddChannelToBridgeException);
            }

            AddedToBridge.Add((bridgeId, channelId));
            Operations.Add($"add:{bridgeId}:{channelId}");

            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            if (HangupExceptionChannelId is not null &&
                string.Equals(HangupExceptionChannelId, channelId, StringComparison.Ordinal))
            {
                return Task.FromException(new AsteriskAriException(
                    nameof(IAsteriskAriClient.HangupAsync),
                    statusCode: null,
                    "Asterisk ARI timed out before the hangup was confirmed.",
                    new HttpRequestException("Asterisk ARI could not reach Asterisk.")));
            }

            HungupChannels.Add(channelId);
            Operations.Add($"hangup:{channelId}");

            return Task.CompletedTask;
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<AsteriskAriLiveRecording> StartBridgeRecordingAsync(string bridgeId, string recordingName, string format, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriLiveRecording { Name = recordingName, Format = format });
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
    }

    private sealed class TestTransferBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];
        private readonly List<string> _operations;

        public TestTransferBindingStore(List<string> operations = null)
        {
            _operations = operations;
        }

        public void Seed(AsteriskChannelTenantBinding binding)
        {
            _bindings.Add(binding);
        }

        public AsteriskChannelTenantBinding Find(string channelId)
        {
            return _bindings.Find(binding => binding.ChannelId == channelId);
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(_bindings.ToArray());
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bindings.Count > 0);
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
        {
            return Task.FromResult(_bindings.Find(binding => binding.ChannelId == channelId));
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(
                _bindings.Where(binding => binding.PeerChannelId == peerChannelId).ToArray());
        }

        public bool SwapAlwaysFails { get; set; }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            // Model the serialized exactly-once create: a concurrent duplicate transfer that races to claim the same
            // deterministic destination channel id loses this create and must return false.
            if (_bindings.Exists(existing => existing.ChannelId == binding.ChannelId))
            {
                return Task.FromResult(false);
            }

            _bindings.Add(binding);
            _operations?.Add($"createBinding:{binding.ChannelId}");

            return Task.FromResult(true);
        }

        public Task<bool> SwapConnectedOwnerAsync(string newAgentChannelId, string previousAgentChannelId)
        {
            // Model the atomic handoff: promote the destination's Joining binding to Connected AND retire the previous
            // agent leg to a non-owning Terminating (Joining-disposition) recovery record in one transaction. If the
            // destination binding is no longer Joining, or the previous leg is no longer the Connected owner, the swap
            // does not commit and neither binding is changed.
            if (SwapAlwaysFails)
            {
                return Task.FromResult(false);
            }

            var promoted = _bindings.Find(binding => binding.ChannelId == newAgentChannelId);

            if (promoted is null || promoted.State != AsteriskChannelBindingState.Joining)
            {
                return Task.FromResult(false);
            }

            // The promotion commits ONLY when the previous agent leg is still the live Connected owner. A missing or
            // non-Connected previous binding means ownership was already swapped away, so promoting this destination
            // would create a second Connected owner: reject instead.
            var previous = _bindings.Find(binding => binding.ChannelId == previousAgentChannelId);

            if (previous is null || previous.State != AsteriskChannelBindingState.Connected)
            {
                return Task.FromResult(false);
            }

            promoted.State = AsteriskChannelBindingState.Connected;
            previous.State = AsteriskChannelBindingState.Terminating;
            previous.PreTeardownState = AsteriskChannelBindingState.Joining;
            _operations?.Add($"swap:{newAgentChannelId}:{previousAgentChannelId}");

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);
            _operations?.Add($"removeBinding:{channelId}");

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TryPromoteJoiningToParticipatingAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Joining)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Participating;
            _operations?.Add($"promoteParticipating:{channelId}");

            return Task.FromResult(true);
        }

        public Task<bool> TryHandOffBridgeOwnershipAsync(string bridgeId, string departingOwnerChannelId)
        {
            var owner = _bindings.Find(item => item.ChannelId == departingOwnerChannelId);

            if (owner is null ||
                (owner.State == AsteriskChannelBindingState.Terminating &&
                    owner.PreTeardownState == AsteriskChannelBindingState.Joining))
            {
                return Task.FromResult(true);
            }

            var participant = _bindings.Find(item =>
                item.State == AsteriskChannelBindingState.Participating &&
                item.BridgeId == bridgeId &&
                item.ChannelId != departingOwnerChannelId);

            if (participant is null)
            {
                return Task.FromResult(false);
            }

            participant.State = AsteriskChannelBindingState.Connected;
            owner.PreTeardownState = AsteriskChannelBindingState.Joining;
            _operations?.Add($"handoffOwner:{participant.ChannelId}");

            return Task.FromResult(true);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            // Model the durable teardown claim: a live binding is marked Terminating and records the state it held
            // before teardown, so a retained provisional transfer claim survives as a reconciler-owned recovery
            // record instead of being deleted.
            var binding = _bindings.Find(existing => existing.ChannelId == channelId);

            if (binding is null || binding.State == AsteriskChannelBindingState.Terminating)
            {
                return Task.FromResult<AsteriskChannelTeardownClaim>(null);
            }

            var previousState = binding.State;
            binding.State = AsteriskChannelBindingState.Terminating;
            binding.PreTeardownState = previousState;
            _operations?.Add($"beginTeardown:{channelId}");

            return Task.FromResult(new AsteriskChannelTeardownClaim
            {
                Binding = binding,
                PreviousState = previousState,
            });
        }
    }
}
