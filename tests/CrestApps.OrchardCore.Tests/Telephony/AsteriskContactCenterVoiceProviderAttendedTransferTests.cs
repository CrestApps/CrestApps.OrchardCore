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

public sealed class AsteriskContactCenterVoiceProviderAttendedTransferTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _callerChannelId = "caller-1";
    private const string _interactionId = "interaction-1";
    private const string _initiatingAgentUserId = "agent-1";
    private const string _destinationAgentUserId = "agent-2";
    private const string _initiatingAgentChannelId = "crestapps-agent-interaction-1-command-1";
    private static readonly string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-command-1";
    private static readonly string _consultChannelId = AsteriskAriConstants.AttendedConsultChannelPrefix + "interaction-1-agent-2";

    [Fact]
    public async Task BeginConsultAsync_WhenDestinationAnswers_HoldsCustomerThenAddsConsultParticipant()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.BeginConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);

        // The customer is held BEFORE the destination agent is rung, so the private consult can never be overheard.
        Assert.Contains(_callerChannelId, ariClient.HeldChannels);
        Assert.DoesNotContain(_callerChannelId, ariClient.UnheldChannels);

        var holdIndex = ariClient.Operations.IndexOf($"hold:{_callerChannelId}");
        var originateIndex = ariClient.Operations.IndexOf($"originate:{_consultChannelId}");
        Assert.True(holdIndex >= 0 && originateIndex >= 0);
        Assert.True(holdIndex < originateIndex, "The customer must be held before the destination agent is rung.");

        // The destination joins the SAME canonical bridge as a non-owning participant; the initiating agent keeps it.
        Assert.Contains((_mixingBridgeId, _consultChannelId), ariClient.AddedToBridge);

        var consult = bindingStore.Find(_consultChannelId);
        Assert.NotNull(consult);
        Assert.Equal(AsteriskChannelBindingState.Participating, consult.State);
        Assert.Equal(_callerChannelId, consult.PeerChannelId);

        var initiating = bindingStore.Find(_initiatingAgentChannelId);
        Assert.NotNull(initiating);
        Assert.Equal(AsteriskChannelBindingState.Connected, initiating.State);

        Assert.Equal(_consultChannelId, result.Metadata[AsteriskVoiceResultMetadata.AttendedTransferConsultChannelId]);
        Assert.Equal(_mixingBridgeId, result.Metadata[AsteriskVoiceResultMetadata.AttendedTransferBridgeId]);
    }

    [Fact]
    public async Task BeginConsultAsync_WhenCallNotOwned_FailsClosedWithoutHoldingOrRinging()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = new TestAttendedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.BeginConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_call_not_owned", result.ErrorCode);
        Assert.Empty(ariClient.HeldChannels);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task BeginConsultAsync_WhenDestinationMissingFromMetadata_FailsClosed()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        var request = CreateRequest();
        request.Metadata.Clear();

        // Act
        var result = await service.BeginConsultAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_target_missing", result.ErrorCode);
        Assert.Empty(ariClient.HeldChannels);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task BeginConsultAsync_WhenHoldFails_FailsClosedWithoutRinging()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient
        {
            HoldException = new InvalidOperationException("hold rejected"),
        };
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.BeginConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_hold_failed", result.ErrorCode);

        // The destination is never rung when the customer cannot be held, so the original call is left intact.
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Null(bindingStore.Find(_consultChannelId));
    }

    [Fact]
    public async Task BeginConsultAsync_WhenDestinationOffline_ResumesCustomerWithoutRinging()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.BeginConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_target_offline", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);

        // The customer was held to prepare the consult, so an offline destination must resume them with the agent.
        Assert.Contains(_callerChannelId, ariClient.HeldChannels);
        Assert.Contains(_callerChannelId, ariClient.UnheldChannels);
    }

    [Fact]
    public async Task BeginConsultAsync_WhenDestinationDoesNotAnswer_HangsUpConsultAndResumesCustomer()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(
            ariClient,
            bindingStore,
            CreateDestinationLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(ready: false));

        // Act
        var result = await service.BeginConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_no_answer", result.ErrorCode);

        // The ringing consult leg is compensated and the customer resumed; the initiating agent keeps the call.
        Assert.Contains(_consultChannelId, ariClient.HungupChannels);
        Assert.Contains(_callerChannelId, ariClient.UnheldChannels);
        Assert.Null(bindingStore.Find(_consultChannelId));

        var initiating = bindingStore.Find(_initiatingAgentChannelId);
        Assert.NotNull(initiating);
        Assert.Equal(AsteriskChannelBindingState.Connected, initiating.State);
    }

    [Fact]
    public async Task CompleteConsultAsync_WhenConsultActive_ResumesCustomerPromotesDestinationRetiresInitiator()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConsultInProgressBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.CompleteConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // The customer is resumed BEFORE the ownership swap, so a lost hand-off leaves them live with the initiator.
        var unholdIndex = ariClient.Operations.IndexOf($"unhold:{_callerChannelId}");
        var swapIndex = ariClient.Operations.IndexOf($"promoteOwner:{_consultChannelId}");
        Assert.True(unholdIndex >= 0 && swapIndex >= 0);
        Assert.True(unholdIndex < swapIndex, "The customer must be resumed before ownership is handed to the destination.");

        // The destination now solely owns the bridge and the initiating leg is retired and hung up.
        var destination = bindingStore.Find(_consultChannelId);
        Assert.NotNull(destination);
        Assert.Equal(AsteriskChannelBindingState.Connected, destination.State);

        Assert.Contains(_initiatingAgentChannelId, ariClient.HungupChannels);
        Assert.Null(bindingStore.Find(_initiatingAgentChannelId));
    }

    [Fact]
    public async Task CompleteConsultAsync_WhenNoLiveConsult_FailsAndKeepsInitiatingAgent()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.CompleteConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_complete_no_target", result.ErrorCode);

        // The initiating agent keeps the call; the customer is still resumed from hold.
        var initiating = bindingStore.Find(_initiatingAgentChannelId);
        Assert.NotNull(initiating);
        Assert.Equal(AsteriskChannelBindingState.Connected, initiating.State);
        Assert.Contains(_callerChannelId, ariClient.UnheldChannels);
        Assert.DoesNotContain(_initiatingAgentChannelId, ariClient.HungupChannels);
    }

    [Fact]
    public async Task CancelConsultAsync_WhenConsultActive_HangsUpConsultAndResumesCustomer()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConsultInProgressBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.CancelConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // The consult leg is dropped and the customer resumed; ownership is unchanged.
        Assert.Contains(_consultChannelId, ariClient.HungupChannels);
        Assert.Contains(_callerChannelId, ariClient.UnheldChannels);
        Assert.Null(bindingStore.Find(_consultChannelId));

        var initiating = bindingStore.Find(_initiatingAgentChannelId);
        Assert.NotNull(initiating);
        Assert.Equal(AsteriskChannelBindingState.Connected, initiating.State);
    }

    [Fact]
    public async Task CancelConsultAsync_WhenCallNotOwned_FailsClosed()
    {
        // Arrange
        var ariClient = new TestAttendedAriClient();
        var bindingStore = new TestAttendedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.CancelConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("consult_cancel_call_not_owned", result.ErrorCode);
        Assert.Empty(ariClient.HungupChannels);
        Assert.Empty(ariClient.UnheldChannels);
    }

    [Fact]
    public async Task CancelConsultAsync_WhenConsultAlreadyPromotedToConnectedOwner_DoesNotHangUpAndKeepsCustomer()
    {
        // Arrange
        // A stale/duplicate cancel arrives after CompleteConsultAsync already promoted the deterministic consult channel
        // in place to the sole Connected owner of the canonical bridge (the initiating leg was retired). Hanging that leg
        // up would tear down the shared bridge and DROP the live customer, so cancel must be a benign no-op here.
        var ariClient = new TestAttendedAriClient();
        var bindingStore = CreateConsultCompletedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateDestinationLeaseStore());

        // Act
        var result = await service.CancelConsultAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // The promoted owner is NEVER hung up, so the live customer is not dropped.
        Assert.DoesNotContain(_consultChannelId, ariClient.HungupChannels);

        var owner = bindingStore.Find(_consultChannelId);
        Assert.NotNull(owner);
        Assert.Equal(AsteriskChannelBindingState.Connected, owner.State);
    }

    private static ContactCenterVoiceAttendedTransferRequest CreateRequest()
    {
        return new ContactCenterVoiceAttendedTransferRequest
        {
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.AttendedTransferMetadata.AgentUserId] = _destinationAgentUserId,
            },
        };
    }

    private static TestAttendedBindingStore CreateConnectedBindingStore(List<string> operations = null)
    {
        var store = new TestAttendedBindingStore(operations);
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _initiatingAgentChannelId,
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

    private static TestAttendedBindingStore CreateConsultInProgressBindingStore(List<string> operations = null)
    {
        var store = CreateConnectedBindingStore(operations);
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _consultChannelId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _mixingBridgeId,
            State = AsteriskChannelBindingState.Participating,
            CreatedUtc = _now,
        });

        return store;
    }

    private static TestAttendedBindingStore CreateConsultCompletedBindingStore(List<string> operations = null)
    {
        // Post-complete topology: the deterministic consult channel is now the sole Connected owner of the canonical
        // bridge and the initiating agent leg has been retired (removed), exactly as PromoteParticipantToConnectedOwnerAsync
        // leaves the store after a successful CompleteConsultAsync.
        var store = new TestAttendedBindingStore(operations);
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _consultChannelId,
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

    private static FakeAsteriskPjsipCredentialLeaseStore CreateDestinationLeaseStore()
    {
        return new FakeAsteriskPjsipCredentialLeaseStore(new AsteriskPjsipCredentialLease
        {
            UserId = _destinationAgentUserId,
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

    private sealed class TestAttendedAriClient : IAsteriskAriClient
    {
        public Exception HoldException { get; set; }

        public string OriginatedChannelId { get; private set; }

        public string OriginatedEndpoint { get; private set; }

        public List<(string BridgeId, string ChannelId)> AddedToBridge { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public List<string> HeldChannels { get; } = [];

        public List<string> UnheldChannels { get; } = [];

        public List<string> Operations { get; } = [];

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
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
            Operations.Add($"createBridge:{bridgeId}");

            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
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

        public Task HoldChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            if (HoldException is not null)
            {
                return Task.FromException(HoldException);
            }

            HeldChannels.Add(channelId);
            Operations.Add($"hold:{channelId}");

            return Task.CompletedTask;
        }

        public Task UnholdChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            UnheldChannels.Add(channelId);
            Operations.Add($"unhold:{channelId}");

            return Task.CompletedTask;
        }
    }

    private sealed class TestAttendedBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];
        private readonly List<string> _operations;

        public TestAttendedBindingStore(List<string> operations = null)
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

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            if (_bindings.Exists(existing => existing.ChannelId == binding.ChannelId))
            {
                return Task.FromResult(false);
            }

            _bindings.Add(binding);
            _operations?.Add($"createBinding:{binding.ChannelId}");

            return Task.FromResult(true);
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

        public Task<bool> SwapConnectedOwnerAsync(string newAgentChannelId, string previousAgentChannelId)
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
            return Task.FromResult(false);
        }

        public Task<bool> TryClaimProvisionalLegForTeardownAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null ||
                (binding.State != AsteriskChannelBindingState.Joining &&
                 binding.State != AsteriskChannelBindingState.Participating))
            {
                return Task.FromResult(false);
            }

            binding.PreTeardownState = binding.State;
            binding.State = AsteriskChannelBindingState.Terminating;
            _operations?.Add($"claimProvisionalTeardown:{channelId}");

            return Task.FromResult(true);
        }

        public Task<bool> PromoteParticipantToConnectedOwnerAsync(string participantChannelId, string previousAgentChannelId)
        {
            var participant = _bindings.Find(item => item.ChannelId == participantChannelId);

            if (participant is null || participant.State != AsteriskChannelBindingState.Participating)
            {
                return Task.FromResult(false);
            }

            var previous = _bindings.Find(item => item.ChannelId == previousAgentChannelId);

            if (previous is null || previous.State != AsteriskChannelBindingState.Connected)
            {
                return Task.FromResult(false);
            }

            participant.State = AsteriskChannelBindingState.Connected;
            previous.State = AsteriskChannelBindingState.Terminating;
            previous.PreTeardownState = AsteriskChannelBindingState.Joining;
            _operations?.Add($"promoteOwner:{participantChannelId}");

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);
            _operations?.Add($"removeBinding:{channelId}");

            return Task.CompletedTask;
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
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
