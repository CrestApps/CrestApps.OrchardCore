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

public sealed class AsteriskContactCenterVoiceProviderConferenceTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _callerChannelId = "caller-1";
    private const string _interactionId = "interaction-1";
    private const string _ownerAgentUserId = "agent-1";
    private const string _participantAgentUserId = "agent-2";
    private const string _ownerAgentChannelId = "crestapps-agent-interaction-1-command-1";
    private static readonly string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-command-1";
    private static readonly string _participantChannelId = AsteriskAriConstants.ConferenceParticipantChannelPrefix + "interaction-1-agent-2";

    [Fact]
    public async Task ConferenceAsync_WhenParticipantAnswers_AddsToCanonicalBridgeAndStabilizesAsParticipating()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = CreateConnectedBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateParticipantLeaseStore());

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);

        Assert.Equal(_participantChannelId, ariClient.OriginatedChannelId);
        Assert.Equal("PJSIP/agent2-endpoint", ariClient.OriginatedEndpoint);
        Assert.Contains((_mixingBridgeId, _participantChannelId), ariClient.AddedToBridge);

        // A participant joins the SAME canonical conversation bridge, so no new bridge is created and nobody is hung up.
        Assert.Empty(ariClient.CreatedBridges);
        Assert.Empty(ariClient.HungupChannels);

        // The participant leg is a stable, NON-owning member of the shared bridge; the original owner keeps the bridge.
        var participant = bindingStore.Find(_participantChannelId);
        Assert.NotNull(participant);
        Assert.Equal(AsteriskChannelBindingState.Participating, participant.State);
        Assert.Equal(_mixingBridgeId, participant.BridgeId);
        Assert.Equal(_callerChannelId, participant.PeerChannelId);

        var owner = bindingStore.Find(_ownerAgentChannelId);
        Assert.NotNull(owner);
        Assert.Equal(AsteriskChannelBindingState.Connected, owner.State);

        // The exactly-once Joining claim is persisted BEFORE the participant leg is originated, so a concurrent
        // duplicate add loses the serialized create and can never re-ring the participant.
        var claimIndex = ariClient.Operations.IndexOf($"createBinding:{_participantChannelId}");
        var originateIndex = ariClient.Operations.IndexOf($"originate:{_participantChannelId}");
        Assert.True(claimIndex >= 0 && originateIndex >= 0);
        Assert.True(claimIndex < originateIndex, "The durable conference claim must be persisted before the participant leg is originated.");

        // The leg joins the canonical bridge BEFORE it is promoted to the stable Participating phase, so any death
        // before it is bridged is a non-owning teardown no-op that leaves the conversation intact.
        var addIndex = ariClient.Operations.IndexOf($"add:{_mixingBridgeId}:{_participantChannelId}");
        var promoteIndex = ariClient.Operations.IndexOf($"promoteParticipating:{_participantChannelId}");
        Assert.True(addIndex >= 0 && promoteIndex >= 0);
        Assert.True(addIndex < promoteIndex, "The participant leg must join the canonical bridge before it is stabilized as a participant.");

        Assert.Equal(_participantChannelId, result.Metadata[AsteriskVoiceResultMetadata.ConferenceParticipantChannelId]);
        Assert.Equal(_mixingBridgeId, result.Metadata[AsteriskVoiceResultMetadata.ConferenceBridgeId]);
    }

    [Fact]
    public async Task ConferenceAsync_WhenCallNotOwned_FailsClosedWithoutOriginating()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = new TestConferenceBindingStore(ariClient.Operations);
        var service = CreateService(ariClient, bindingStore, CreateParticipantLeaseStore());

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("conference_call_not_owned", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task ConferenceAsync_WhenParticipantMissingFromMetadata_FailsClosed()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore, CreateParticipantLeaseStore());

        var request = CreateRequest();
        request.Metadata.Clear();

        // Act
        var result = await service.ConferenceAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("conference_target_missing", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task ConferenceAsync_WhenParticipantOffline_FailsClosedWithoutOriginating()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore, new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("conference_target_offline", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task ConferenceAsync_WhenParticipantDoesNotAnswer_HangsUpLegAndLeavesConversationIntact()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(
            ariClient,
            bindingStore,
            CreateParticipantLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(ready: false));

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("conference_no_answer", result.ErrorCode);

        // The ringing participant leg is hung up and its confirmed-gone claim is hard-removed so a retry can re-run.
        Assert.Contains(_participantChannelId, ariClient.HungupChannels);
        Assert.Null(bindingStore.Find(_participantChannelId));

        // The participant never joined the bridge, so the existing conversation is fully intact.
        Assert.DoesNotContain((_mixingBridgeId, _participantChannelId), ariClient.AddedToBridge);
        var owner = bindingStore.Find(_ownerAgentChannelId);
        Assert.NotNull(owner);
        Assert.Equal(AsteriskChannelBindingState.Connected, owner.State);
    }

    [Fact]
    public async Task ConferenceAsync_WhenRetriedForSameParticipant_IsIdempotentWithoutReOriginating()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient();
        var bindingStore = CreateConnectedBindingStore();

        // A prior add already stabilized the participant as a live Participating member of the shared bridge.
        bindingStore.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _participantChannelId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _mixingBridgeId,
            State = AsteriskChannelBindingState.Participating,
            CreatedUtc = _now,
        });

        var service = CreateService(ariClient, bindingStore, CreateParticipantLeaseStore());

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // The participant is already in the call, so no second leg is originated.
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Empty(ariClient.AddedToBridge);
        Assert.Equal(_participantChannelId, result.Metadata[AsteriskVoiceResultMetadata.ConferenceParticipantChannelId]);
    }

    [Fact]
    public async Task ConferenceAsync_WhenAddToBridgeIsAmbiguous_CompensatesParticipantLegAndLeavesConversationIntact()
    {
        // Arrange
        var ariClient = new TestConferenceAriClient
        {
            // The participant answered, but adding it to the canonical bridge timed out with no observed response, so
            // the outcome is ambiguous and the possibly-live participant leg must be compensated.
            AddChannelToBridgeException = new AsteriskAriException(
                nameof(IAsteriskAriClient.AddChannelToBridgeAsync),
                statusCode: null,
                "Asterisk ARI timed out before the bridge add was confirmed.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore, CreateParticipantLeaseStore());

        // Act
        var result = await service.ConferenceAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("conference_outcome_unknown", result.ErrorCode);

        // The possibly-live participant leg is compensated (hung up) and, once its hangup is confirmed, its claim is
        // hard-removed; the existing conversation is never disturbed because a Joining leg never owns the bridge.
        Assert.Contains(_participantChannelId, ariClient.HungupChannels);
        Assert.Null(bindingStore.Find(_participantChannelId));

        var owner = bindingStore.Find(_ownerAgentChannelId);
        Assert.NotNull(owner);
        Assert.Equal(AsteriskChannelBindingState.Connected, owner.State);
    }

    private static ContactCenterVoiceConferenceRequest CreateRequest()
    {
        return new ContactCenterVoiceConferenceRequest
        {
            InteractionId = _interactionId,
            ProviderCallIds = [_callerChannelId],
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.ConferenceMetadata.AgentUserId] = _participantAgentUserId,
            },
        };
    }

    private static TestConferenceBindingStore CreateConnectedBindingStore(List<string> operations = null)
    {
        var store = new TestConferenceBindingStore(operations);
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _ownerAgentChannelId,
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

    private static FakeAsteriskPjsipCredentialLeaseStore CreateParticipantLeaseStore()
    {
        return new FakeAsteriskPjsipCredentialLeaseStore(new AsteriskPjsipCredentialLease
        {
            UserId = _participantAgentUserId,
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

    private sealed class TestConferenceAriClient : IAsteriskAriClient
    {
        public Exception AddChannelToBridgeException { get; set; }

        public string OriginatedChannelId { get; private set; }

        public string OriginatedEndpoint { get; private set; }

        public List<(string BridgeId, string ChannelId)> AddedToBridge { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public List<string> CreatedBridges { get; } = [];

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
            return Task.CompletedTask;
        }

        public Task UnholdChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestConferenceBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];
        private readonly List<string> _operations;

        public TestConferenceBindingStore(List<string> operations = null)
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

        public Task<bool> PromoteParticipantToConnectedOwnerAsync(string participantChannelId, string previousAgentChannelId)
        {
            return Task.FromResult(false);
        }
    }
}
