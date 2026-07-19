using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceProviderConnectSuccessTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _commandId = "command-1";

    // The connect flow derives every ARI resource id from the stable key (interaction id + per-acceptance command id),
    // so the agent channel and mixing bridge ids are deterministic and known up front for a given request.
    private const string _agentChannelId = AsteriskAriConstants.AgentChannelPrefix + "interaction-1-" + _commandId;
    private const string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-" + _commandId;

    [Fact]
    public async Task ConnectToAgentAsync_WhenAsteriskOperationsSucceed_ReturnsConnectedAgentChannelMetadata()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel
            {
                Id = "agent-chan-1",
            },
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = CommandMetadata(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("caller-1", result.ProviderCallId);
        Assert.Equal(_agentChannelId, result.Metadata[AsteriskAriConstants.AgentChannelMetadataKey]);
        Assert.Equal(_mixingBridgeId, result.Metadata["bridgeId"]);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.Equal(_agentChannelId, bindingStore.CreatedBinding.ChannelId);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, bindingStore.CreatedBinding.ProviderName);
        Assert.Equal("caller-1", bindingStore.CreatedBinding.ProviderCallId);
        Assert.Equal("interaction-1", bindingStore.CreatedBinding.InteractionId);
        Assert.Equal(_now, bindingStore.CreatedBinding.CreatedUtc);
        Assert.NotNull(ariClient.OriginateRequest);
        Assert.Equal(_agentChannelId, ariClient.OriginateRequest.ChannelId);
        Assert.Equal("PJSIP/agent1", ariClient.OriginateRequest.Endpoint);
        Assert.Equal("caller-1", ariClient.OriginateRequest.CallerId);
        Assert.Contains(ariClient.AddedChannels, call => call.ChannelId == "caller-1");
        Assert.Contains(ariClient.AddedChannels, call => call.ChannelId == _agentChannelId);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenAgentEndpointNotSupplied_ResolvesLivePjsipRegistration()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel
            {
                Id = "agent-chan-1",
            },
        };
        var bindingStore = new TestBindingStore();
        var leaseStore = new FakeAsteriskPjsipCredentialLeaseStore(
            new AsteriskPjsipCredentialLease
            {
                UserId = "agent-user-1",
                AuthorizationUser = "agentauth-old",
                IssuedUtc = _now.AddMinutes(-10),
                ExpiresUtc = _now.AddMinutes(20),
            },
            new AsteriskPjsipCredentialLease
            {
                UserId = "agent-user-1",
                AuthorizationUser = "agentauth-new",
                IssuedUtc = _now,
                ExpiresUtc = _now.AddMinutes(30),
            });
        var service = CreateService(ariClient, bindingStore, leaseStore);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentId = "agent-1",
            AgentUserId = "agent-user-1",
            InteractionId = "interaction-1",
            Metadata = CommandMetadata(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-user-1", leaseStore.LastListLiveUserId);
        Assert.NotNull(ariClient.OriginateRequest);
        Assert.Equal("PJSIP/agentauth-new", ariClient.OriginateRequest.Endpoint);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenAgentHasNoLiveRegistration_FailsClosed()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(ariClient, bindingStore, new FakeAsteriskPjsipCredentialLeaseStore());

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentId = "agent-1",
            AgentUserId = "agent-user-1",
            InteractionId = "interaction-1",
            Metadata = CommandMetadata(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_endpoint_missing", result.ErrorCode);
        Assert.Null(ariClient.OriginateRequest);
        Assert.Empty(ariClient.AddedChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenAgentDoesNotAnswerBeforeTimeout_FailsAndCompensates()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel
            {
                Id = "agent-chan-1",
            },
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(
            ariClient,
            bindingStore,
            agentChannelReadySignal: new FakeAsteriskAgentChannelReadySignal(ready: false));

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = CommandMetadata(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_no_answer", result.ErrorCode);
        Assert.NotNull(ariClient.OriginateRequest);
        Assert.Empty(ariClient.AddedChannels);
        Assert.Contains(_agentChannelId, ariClient.HungupChannels);
        Assert.NotEmpty(ariClient.DestroyedBridges);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.Contains(_agentChannelId, bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ConnectToAgentAsync_PersistsBindingBeforeExposingLiveBridge()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel
            {
                Id = "agent-chan-1",
            },
        };
        var bindingStore = new TestBindingStore();
        bindingStore.AddedChannelCountProvider = () => ariClient.AddedChannels.Count;
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = CommandMetadata(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(bindingStore.CreatedBinding);
        Assert.NotNull(bindingStore.AddedChannelCountAtCreate);
        Assert.Equal(0, bindingStore.AddedChannelCountAtCreate.Value);
        Assert.Equal(2, ariClient.AddedChannels.Count);
    }

    private static Dictionary<string, string> CommandMetadata()
    {
        return new Dictionary<string, string>
        {
            [ContactCenterConstants.CommandMetadata.CommandId] = _commandId,
        };
    }

    private static AsteriskContactCenterVoiceProvider CreateService(
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore bindingStore,
        IAsteriskPjsipCredentialLeaseStore pjsipCredentialLeaseStore = null,
        IAsteriskAgentChannelReadySignal agentChannelReadySignal = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore,
            pjsipCredentialLeaseStore ?? new FakeAsteriskPjsipCredentialLeaseStore(),
            agentChannelReadySignal ?? new FakeAsteriskAgentChannelReadySignal(),
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

    private sealed class TestAriClient : IAsteriskAriClient
    {
        public HashSet<string> ExistingChannels { get; set; } = [];

        public AsteriskAriChannel OriginateChannel { get; set; }

        public AsteriskAriOriginateRequest OriginateRequest { get; private set; }

        public List<(string BridgeId, string ChannelId)> AddedChannels { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public List<string> DestroyedBridges { get; } = [];

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            OriginateRequest = request;

            return Task.FromResult(OriginateChannel);
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriBridge
            {
                Id = bridgeId,
            });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            AddedChannels.Add((bridgeId, channelId));

            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannels.Add(channelId);

            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingChannels.Contains(channelId));
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            DestroyedBridges.Add(bridgeId);

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

    private sealed class TestBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];

        public AsteriskChannelTenantBinding CreatedBinding { get; private set; }

        public int CreateCount { get; private set; }

        public Func<int> AddedChannelCountProvider { get; set; }

        public int? AddedChannelCountAtCreate { get; private set; }

        public List<string> RemovedChannelIds { get; } = [];

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
            CreateCount++;
            CreatedBinding = binding;
            _bindings.Add(binding);
            AddedChannelCountAtCreate = AddedChannelCountProvider?.Invoke();

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            RemovedChannelIds.Add(channelId);
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Connected;

            return Task.FromResult(true);
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Offering)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Connected;

            return Task.FromResult(true);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
            {
                return Task.FromResult(false);
            }

            binding.CallerDetached = true;

            return Task.FromResult(true);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            var binding = _bindings.Find(item => item.ChannelId == channelId);

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
    }
}
