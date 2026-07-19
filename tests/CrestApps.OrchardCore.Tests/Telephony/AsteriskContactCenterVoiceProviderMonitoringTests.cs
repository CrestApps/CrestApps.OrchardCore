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

public sealed class AsteriskContactCenterVoiceProviderMonitoringTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _callerChannelId = "caller-1";
    private const string _interactionId = "interaction-1";
    private const string _supervisorId = "sup-1";
    private const string _agentChannelId = "crestapps-agent-interaction-1-command-1";
    private static readonly string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-command-1";

    private static readonly string _stableKey = _interactionId + "-" + _supervisorId;
    private static readonly string _supervisorBridgeId = AsteriskAriConstants.SupervisorBridgePrefix + _stableKey;
    private static readonly string _supervisorChannelId = AsteriskAriConstants.SupervisorChannelPrefix + _stableKey;
    private static readonly string _snoopChannelId = AsteriskAriConstants.SupervisorSnoopPrefix + _stableKey;

    [Fact]
    public async Task EngageAsync_WhenMonitor_SnoopsAgentListenOnlyAndBridgesSupervisor()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);

        Assert.Equal(_agentChannelId, ariClient.SnoopedChannelId);
        Assert.Equal(AsteriskAriConstants.SnoopSpyBoth, ariClient.SnoopSpy);
        Assert.Equal(AsteriskAriConstants.SnoopWhisperNone, ariClient.SnoopWhisper);
        Assert.Equal(_snoopChannelId, ariClient.SnoopId);

        Assert.Contains((_supervisorBridgeId, AsteriskAriConstants.MixingBridgeType), ariClient.CreatedBridges);
        Assert.Equal(_supervisorChannelId, ariClient.OriginatedChannelId);
        Assert.Contains((_supervisorBridgeId, _snoopChannelId), ariClient.AddedToBridge);
        Assert.Contains((_supervisorBridgeId, _supervisorChannelId), ariClient.AddedToBridge);

        Assert.Equal(_supervisorChannelId, result.Metadata[ContactCenterConstants.MonitoringMetadata.SupervisorChannelId]);
        Assert.Equal(_snoopChannelId, result.Metadata[ContactCenterConstants.MonitoringMetadata.SnoopChannelId]);
        Assert.Equal(_supervisorBridgeId, result.Metadata[ContactCenterConstants.MonitoringMetadata.BridgeId]);
        Assert.Equal(nameof(MonitorMode.Monitor), result.Metadata[ContactCenterConstants.MonitoringMetadata.Mode]);
    }

    [Fact]
    public async Task EngageAsync_WhenWhisper_SnoopsAgentWithOutboundWhisper()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Whisper),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_agentChannelId, ariClient.SnoopedChannelId);
        Assert.Equal(AsteriskAriConstants.SnoopSpyBoth, ariClient.SnoopSpy);
        Assert.Equal(AsteriskAriConstants.SnoopWhisperOut, ariClient.SnoopWhisper);
        Assert.Contains((_supervisorBridgeId, _snoopChannelId), ariClient.AddedToBridge);
        Assert.Contains((_supervisorBridgeId, _supervisorChannelId), ariClient.AddedToBridge);
    }

    [Fact]
    public async Task EngageAsync_WhenBarge_AddsSupervisorToMixingBridgeWithoutSnoop()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Barge),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(ariClient.SnoopedChannelId);
        Assert.Empty(ariClient.CreatedBridges);
        Assert.Equal(_supervisorChannelId, ariClient.OriginatedChannelId);
        Assert.Contains((_mixingBridgeId, _supervisorChannelId), ariClient.AddedToBridge);

        Assert.Equal(_mixingBridgeId, result.Metadata[ContactCenterConstants.MonitoringMetadata.BridgeId]);
        Assert.False(result.Metadata.ContainsKey(ContactCenterConstants.MonitoringMetadata.SnoopChannelId));
    }

    [Fact]
    public async Task EngageAsync_WhenNoOwningBindingExists_FailsClosed()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, new TestMonitoringBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("monitor_call_not_owned", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Null(ariClient.SnoopedChannelId);
    }

    [Fact]
    public async Task EngageAsync_WhenSupervisorHasNoLiveEndpoint_FailsClosed()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("supervisor_endpoint_missing", result.ErrorCode);
        Assert.Null(ariClient.OriginatedChannelId);
    }

    [Fact]
    public async Task EngageAsync_WhenSupervisorDoesNotAnswer_CompensatesAndFailsClosed()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(
            ariClient,
            CreateConnectedBindingStore(),
            CreateSupervisorLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(ready: false));

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("supervisor_no_answer", result.ErrorCode);
        Assert.Null(ariClient.SnoopedChannelId);
        Assert.Contains(_supervisorChannelId, ariClient.HungupChannels);
        Assert.Contains(_supervisorBridgeId, ariClient.DestroyedBridges);
    }

    [Fact]
    public async Task EngageAsync_WhenAriTimesOutAmbiguously_ReportsOutcomeUnknown()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                statusCode: null,
                "Asterisk ARI timed out before a response was observed.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("monitor_outcome_unknown", result.ErrorCode);
    }

    [Fact]
    public async Task EngageAsync_WhenAriRejects_ReportsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                HttpStatusCode.BadRequest,
                "Asterisk ARI rejected the originate request."),
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("monitor_failed", result.ErrorCode);
    }

    [Fact]
    public async Task EngageAsync_WhenProviderPreflightRejectsWithoutReachingAsterisk_ReportsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient
        {
            OriginateException = new AsteriskAriException(
                nameof(IAsteriskAriClient.OriginateAsync),
                statusCode: null,
                "The Asterisk provider is not configured."),
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("monitor_failed", result.ErrorCode);
    }

    [Fact]
    public async Task StopAsync_WhenListening_HangsUpSupervisorAndSnoopAndDestroysSupervisorBridge()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.StopAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(_supervisorChannelId, ariClient.HungupChannels);
        Assert.Contains(_snoopChannelId, ariClient.HungupChannels);
        Assert.Contains(_supervisorBridgeId, ariClient.DestroyedBridges);
    }

    [Fact]
    public async Task StopAsync_WhenBarge_RemovesSupervisorFromMixingBridgeThenHangsUp()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.StopAsync(
            CreateRequest(MonitorMode.Barge),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains((_mixingBridgeId, _supervisorChannelId), ariClient.RemovedFromBridge);
        Assert.Contains(_supervisorChannelId, ariClient.HungupChannels);
        Assert.Empty(ariClient.DestroyedBridges);
    }

    [Fact]
    public async Task StopAsync_WhenResourcesAlreadyGone_IsIdempotentSuccess()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var first = await service.StopAsync(CreateRequest(MonitorMode.Monitor), TestContext.Current.CancellationToken);
        var second = await service.StopAsync(CreateRequest(MonitorMode.Monitor), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task EngageAsync_WhenSupervisorChannelAlreadyExists_ReassertsTopologyNonDestructively()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();

        // Simulate an already-established engagement: the deterministic supervisor channel already exists, so a
        // retried start must confirm the audio topology by re-asserting the idempotent snoop and bridge operations
        // (existence alone does not prove the topology is complete), yet it must NOT re-originate a new leg or tear
        // anything down.
        ariClient.ExistingChannels.Add(_supervisorChannelId);
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_supervisorChannelId, result.Metadata[ContactCenterConstants.MonitoringMetadata.SupervisorChannelId]);
        Assert.Equal(_snoopChannelId, result.Metadata[ContactCenterConstants.MonitoringMetadata.SnoopChannelId]);
        Assert.Equal(_supervisorBridgeId, result.Metadata[ContactCenterConstants.MonitoringMetadata.BridgeId]);

        // The re-assertion completes/confirms the topology idempotently...
        Assert.Equal(_snoopChannelId, ariClient.SnoopId);
        Assert.Contains((_supervisorBridgeId, AsteriskAriConstants.MixingBridgeType), ariClient.CreatedBridges);
        Assert.Contains((_supervisorBridgeId, _snoopChannelId), ariClient.AddedToBridge);
        Assert.Contains((_supervisorBridgeId, _supervisorChannelId), ariClient.AddedToBridge);

        // ...but it never re-originates the supervisor leg or tears down the live engagement.
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Empty(ariClient.HungupChannels);
        Assert.Empty(ariClient.DestroyedBridges);
    }

    [Fact]
    public async Task EngageAsync_WhenBargeSupervisorChannelAlreadyExists_ReassertsMixingBridgeMembershipNonDestructively()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient();
        ariClient.ExistingChannels.Add(_supervisorChannelId);
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Barge),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_mixingBridgeId, result.Metadata[ContactCenterConstants.MonitoringMetadata.BridgeId]);
        Assert.False(result.Metadata.ContainsKey(ContactCenterConstants.MonitoringMetadata.SnoopChannelId));

        // Barge re-assertion re-confirms the supervisor leg is attached to the main conversation bridge (a no-op
        // when already a member) without snooping, re-originating, or hanging anything up.
        Assert.Contains((_mixingBridgeId, _supervisorChannelId), ariClient.AddedToBridge);
        Assert.Null(ariClient.SnoopId);
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Empty(ariClient.HungupChannels);
        Assert.Empty(ariClient.DestroyedBridges);
    }

    [Fact]
    public async Task EngageAsync_WhenExistenceProbeFailsTransiently_DoesNotTearDownLiveEngagement()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient
        {
            // A transient transport failure of the non-mutating existence probe surfaces as a null-status ARI
            // exception with an inner transport exception (ambiguous outcome). This probe runs against a possibly-live
            // engagement, so its failure must NEVER trigger compensation.
            ChannelExistsException = new AsteriskAriException(
                nameof(IAsteriskAriClient.ChannelExistsAsync),
                statusCode: null,
                "Asterisk ARI could not reach Asterisk.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act
        var result = await service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("monitor_outcome_unknown", result.ErrorCode);

        // The failed probe created nothing, so nothing may be torn down: a transient probe failure must leave a
        // live engagement's supervisor leg, snoop, and bridge completely untouched.
        Assert.Empty(ariClient.HungupChannels);
        Assert.Empty(ariClient.DestroyedBridges);
        Assert.Empty(ariClient.RemovedFromBridge);
        Assert.Null(ariClient.OriginatedChannelId);
        Assert.Null(ariClient.SnoopId);
        Assert.Empty(ariClient.CreatedBridges);
    }

    [Fact]
    public async Task EngageAsync_WhenCancelledAfterResourceCreation_CompensatesSupervisorLegsAndRethrows()
    {
        // Arrange
        var ariClient = new TestMonitoringAriClient
        {
            // The second bridge add (supervisor channel into the supervisor bridge) throws cancellation AFTER the
            // supervisor bridge, snoop, and supervisor channel already exist, so those legs must be compensated.
            AddChannelToBridgeException = new OperationCanceledException(),
            AddChannelToBridgeThrowOnCall = 2,
        };
        var service = CreateService(ariClient, CreateConnectedBindingStore(), CreateSupervisorLeaseStore());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.EngageAsync(
            CreateRequest(MonitorMode.Monitor),
            TestContext.Current.CancellationToken));

        // The supervisor-owned legs are torn down (no leak) and the customer/agent call is untouched.
        Assert.Contains(_supervisorChannelId, ariClient.HungupChannels);
        Assert.Contains(_snoopChannelId, ariClient.HungupChannels);
        Assert.Contains(_supervisorBridgeId, ariClient.DestroyedBridges);
        Assert.DoesNotContain(_agentChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_callerChannelId, ariClient.HungupChannels);
        Assert.DoesNotContain(_mixingBridgeId, ariClient.DestroyedBridges);
    }

    private static ContactCenterVoiceMonitoringRequest CreateRequest(MonitorMode mode)
    {
        return new ContactCenterVoiceMonitoringRequest
        {
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            SupervisorId = _supervisorId,
            Mode = mode,
        };
    }

    private static TestMonitoringBindingStore CreateConnectedBindingStore()
    {
        var store = new TestMonitoringBindingStore();
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = _agentChannelId,
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

    private static FakeAsteriskPjsipCredentialLeaseStore CreateSupervisorLeaseStore()
    {
        return new FakeAsteriskPjsipCredentialLeaseStore(new AsteriskPjsipCredentialLease
        {
            UserId = _supervisorId,
            AuthorizationUser = "super-endpoint",
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

    private sealed class TestMonitoringAriClient : IAsteriskAriClient
    {
        private int _addChannelToBridgeCalls;

        public Exception OriginateException { get; set; }

        public HashSet<string> ExistingChannels { get; } = new(StringComparer.Ordinal);

        public Exception ChannelExistsException { get; set; }

        public Exception AddChannelToBridgeException { get; set; }

        public int AddChannelToBridgeThrowOnCall { get; set; }

        public string OriginatedChannelId { get; private set; }

        public string SnoopedChannelId { get; private set; }

        public string SnoopSpy { get; private set; }

        public string SnoopWhisper { get; private set; }

        public string SnoopId { get; private set; }

        public List<(string BridgeId, string BridgeType)> CreatedBridges { get; } = [];

        public List<(string BridgeId, string ChannelId)> AddedToBridge { get; } = [];

        public List<(string BridgeId, string ChannelId)> RemovedFromBridge { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public List<string> DestroyedBridges { get; } = [];

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            if (OriginateException is not null)
            {
                return Task.FromException<AsteriskAriChannel>(OriginateException);
            }

            OriginatedChannelId = request.ChannelId;

            return Task.FromResult(new AsteriskAriChannel { Id = request.ChannelId });
        }

        public Task<AsteriskAriChannel> SnoopChannelAsync(string channelId, string spy, string whisper, string snoopId, CancellationToken cancellationToken)
        {
            SnoopedChannelId = channelId;
            SnoopSpy = spy;
            SnoopWhisper = whisper;
            SnoopId = snoopId;

            return Task.FromResult(new AsteriskAriChannel { Id = snoopId });
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            CreatedBridges.Add((bridgeId, bridgeType));

            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            _addChannelToBridgeCalls++;

            if (AddChannelToBridgeException is not null && _addChannelToBridgeCalls == AddChannelToBridgeThrowOnCall)
            {
                return Task.FromException(AddChannelToBridgeException);
            }

            AddedToBridge.Add((bridgeId, channelId));

            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            RemovedFromBridge.Add((bridgeId, channelId));

            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannels.Add(channelId);

            return Task.CompletedTask;
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            DestroyedBridges.Add(bridgeId);

            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            if (ChannelExistsException is not null)
            {
                return Task.FromException<bool>(ChannelExistsException);
            }

            return Task.FromResult(ExistingChannels.Contains(channelId));
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
    }

    private sealed class TestMonitoringBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];

        public void Seed(AsteriskChannelTenantBinding binding)
        {
            _bindings.Add(binding);
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
            _bindings.Add(binding);

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);

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

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            return Task.FromResult<AsteriskChannelTeardownClaim>(null);
        }
    }
}
