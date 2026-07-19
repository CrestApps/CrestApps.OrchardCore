using System.Net;
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

public sealed class AsteriskContactCenterVoiceProviderConnectFailureTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string _holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + "caller-1";

    private const string _commandId = "command-1";

    // The connect flow derives every ARI resource id from the stable key (interaction id + per-acceptance command id),
    // so the agent channel and mixing bridge ids are deterministic and known up front for a given request.
    private const string _agentChannelId = AsteriskAriConstants.AgentChannelPrefix + "interaction-1-" + _commandId;
    private const string _mixingBridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-" + _commandId;

    [Fact]
    public async Task ConnectToAgentAsync_WhenBridgingFailsAfterHoldingDetach_ReParksCallerAndDoesNotStrandIt()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            AddChannelShouldThrow = (bridgeId, _) =>
                !bridgeId.StartsWith(AsteriskConstants.HoldingBridgePrefix, StringComparison.Ordinal),
        };
        var service = CreateService(ariClient);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_connect_failed", result.ErrorCode);
        Assert.Contains(ariClient.DestroyedBridges, id => id != _holdingBridgeId);
        Assert.Contains(ariClient.HungUpChannels, id => id == _agentChannelId);
        Assert.Contains(ariClient.CreatedBridges, bridge =>
            bridge.BridgeId == _holdingBridgeId &&
            bridge.BridgeType == AsteriskAriConstants.HoldingBridgeType);
        Assert.Contains(ariClient.AddedChannels, call =>
            call.BridgeId == _holdingBridgeId && call.ChannelId == "caller-1");
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenReParkAlsoFails_HangsUpCallerToAvoidASilentStrandedChannel()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            AddChannelShouldThrow = (_, _) => true,
        };
        var service = CreateService(ariClient);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenOriginateFailsBeforeHoldingDetach_DoesNotReParkOrHangUpCaller()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateShouldThrow = true,
        };
        var service = CreateService(ariClient);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.DoesNotContain(ariClient.CreatedBridges, bridge => bridge.BridgeId == _holdingBridgeId);
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenBridgingFailsAfterBindingPersisted_RemovesBindingDuringCompensation()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            AddChannelShouldThrow = (bridgeId, _) =>
                !bridgeId.StartsWith(AsteriskConstants.HoldingBridgePrefix, StringComparison.Ordinal),
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(_agentChannelId, bindingStore.CreatedChannelIds);
        Assert.Contains(_agentChannelId, bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenBridgingFails_ClaimsPendingBindingBeforeHangingUpAgent()
    {
        // Arrange
        var operations = new List<string>();
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            AddChannelShouldThrow = (bridgeId, _) =>
                !bridgeId.StartsWith(AsteriskConstants.HoldingBridgePrefix, StringComparison.Ordinal),
            OperationsLog = operations,
        };
        var bindingStore = new TestBindingStore { OperationsLog = operations };
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        var beginTeardownIndex = operations.IndexOf("begin-teardown:" + _agentChannelId);
        var hangupIndex = operations.IndexOf("hangup:" + _agentChannelId);

        Assert.True(beginTeardownIndex >= 0, "The pending binding should be claimed for teardown during compensation.");
        Assert.True(hangupIndex >= 0, "The agent channel should be hung up during compensation.");
        Assert.True(
            beginTeardownIndex < hangupIndex,
            "The pending binding must be claimed (flipped to Terminating with a Pending disposition) before the agent hangup so the resulting terminal event is read as an already-claimed leg rather than a connected call that would hang up the caller.");
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenPendingBindingIsRemovedDuringBridging_ReturnsCallerToHoldingWithoutHangingUp()
    {
        // Arrange
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
        };
        var bindingStore = new TestBindingStore { MarkConnectedResult = false };
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_connect_lost", result.ErrorCode);
        Assert.Contains(ariClient.CreatedBridges, bridge =>
            bridge.BridgeId == _holdingBridgeId &&
            bridge.BridgeType == AsteriskAriConstants.HoldingBridgeType);
        Assert.Contains(ariClient.AddedChannels, call =>
            call.BridgeId == _holdingBridgeId && call.ChannelId == "caller-1");
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenMixingBridgeCreateOutcomeIsAmbiguous_BestEffortCleansButRetainsRecordForReconciler()
    {
        // Arrange
        // Model a transport-ambiguous ack loss: the create-bridge call fails without a server response (a null status
        // code), so the client cannot know whether Asterisk created the deterministic bridge. Compensation must still
        // best-effort tear down the resources this attempt may have materialized, but because the outcome is unknown it
        // MUST retain the durable record: a compensation destroy that 404s here cannot prove the bridge is absent (it
        // may still be committing), so only the age-gated reconciler — which re-probes live ARI state — may retire it.
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            CreateBridgeShouldThrow = (bridgeId, _) => bridgeId == _mixingBridgeId,
            CreateBridgeException = new AsteriskAriException("createBridge", null, "createBridge transport ack lost"),
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown, "A lost create-bridge ack must surface as an unknown outcome.");

        // Compensation still best-effort destroys the possibly-live bridge and hangs up the pre-created agent leg.
        Assert.Contains(_mixingBridgeId, ariClient.DestroyedBridges);
        Assert.Contains(_agentChannelId, bindingStore.CreatedChannelIds);
        Assert.Contains(_agentChannelId, ariClient.HungUpChannels);

        // The outcome is ambiguous, so the durable record is RETAINED for the reconciler rather than deleted on the
        // strength of a compensation that cannot prove the resource is gone.
        Assert.DoesNotContain(_agentChannelId, bindingStore.RemovedChannelIds);

        // Bridging never started (the mixing bridge create failed first), so the caller was never detached from
        // holding and is neither re-parked nor hung up.
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenMixingBridgeCreateIsRejectedWithClientError_CompensatesAndRetiresRecord()
    {
        // Arrange
        // A definite 4xx client-rejection proves the create-bridge operation did not take effect, so there is no
        // ambiguous resource to reconcile: compensation may safely tear down the pre-created agent leg and retire the
        // durable record immediately, exactly as a normal (non-ambiguous) provisioning failure would.
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
            CreateBridgeShouldThrow = (bridgeId, _) => bridgeId == _mixingBridgeId,
            CreateBridgeException = new AsteriskAriException("createBridge", HttpStatusCode.BadRequest, "rejected"),
        };
        var bindingStore = new TestBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(_agentChannelId, bindingStore.CreatedChannelIds);
        Assert.Contains(_agentChannelId, ariClient.HungUpChannels);
        Assert.Contains(_agentChannelId, bindingStore.RemovedChannelIds);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenPendingBindingClaimedByTeardown_StillSelfCleansItsOwnBridgeAndAgentLeg()
    {
        // Arrange
        // A terminal-event teardown durably claims the pending agent leg first (MarkConnectedResult=false flips the
        // record to Terminating), so the connect flow loses the finalize race. Even though it no longer owns the
        // record, it MUST still destroy its own deterministic mixing bridge and agent leg — their ids are unique to
        // this attempt (the command-id fence), so self-cleaning them can never touch another attempt and closes the
        // leak where a racing teardown that 404'd on not-yet-created resources leaves them orphaned.
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
        };
        var bindingStore = new TestBindingStore { MarkConnectedResult = false };
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_connect_lost", result.ErrorCode);

        // Self-clean of this attempt's own deterministic resources happens regardless of losing the claim.
        Assert.Contains(_agentChannelId, ariClient.HungUpChannels);
        Assert.Contains(_mixingBridgeId, ariClient.DestroyedBridges);

        // The record is owned by the teardown that claimed it, so this flow must NOT retire it.
        Assert.DoesNotContain(_agentChannelId, bindingStore.RemovedChannelIds);

        // The caller is re-parked for re-offer (a pending-disposition teardown never releases it) and not hung up.
        Assert.Contains(ariClient.CreatedBridges, bridge =>
            bridge.BridgeId == _holdingBridgeId &&
            bridge.BridgeType == AsteriskAriConstants.HoldingBridgeType);
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenPendingLegClaimedBeforeCallerDetach_AbortsWithoutDetachingCaller()
    {
        // Arrange
        // A terminal-event teardown durably claims the pending agent leg BEFORE the connect flow reaches the
        // caller-detach fence (MarkCallerDetachedResult=false). The connect flow must honor the lost compare-and-set
        // and abort WITHOUT detaching the caller from holding: the caller is still safely parked, so detaching it here
        // and then losing the record would strand it outside every bridge. Only this attempt's own deterministic agent
        // leg and mixing bridge are self-cleaned; the caller is never touched and never needs re-parking.
        var ariClient = new TestAriClient
        {
            ExistingChannels = ["caller-1"],
            OriginateChannel = new AsteriskAriChannel { Id = "agent-chan-1" },
        };
        var bindingStore = new TestBindingStore { MarkCallerDetachedResult = false };
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.ConnectToAgentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_connect_lost", result.ErrorCode);

        // Self-clean of this attempt's own deterministic resources happens regardless of losing the claim.
        Assert.Contains(_agentChannelId, ariClient.HungUpChannels);
        Assert.Contains(_mixingBridgeId, ariClient.DestroyedBridges);

        // The record is owned by the teardown that claimed it, so this flow must NOT retire it.
        Assert.DoesNotContain(_agentChannelId, bindingStore.RemovedChannelIds);

        // The caller was never detached from holding: it was never added to the mixing bridge, never hung up, and —
        // because it never left holding — is not re-parked into a fresh holding bridge.
        Assert.DoesNotContain(ariClient.AddedChannels, call =>
            call.BridgeId == _mixingBridgeId && call.ChannelId == "caller-1");
        Assert.DoesNotContain("caller-1", ariClient.HungUpChannels);
        Assert.DoesNotContain(ariClient.CreatedBridges, bridge => bridge.BridgeId == _holdingBridgeId);
    }

    private static ContactCenterConnectRequest CreateRequest()
    {
        return new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = _commandId,
            },
        };
    }

    private static AsteriskContactCenterVoiceProvider CreateService(
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore bindingStore = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore ?? new TestBindingStore(),
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
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

        public bool OriginateShouldThrow { get; set; }

        public Func<string, string, bool> AddChannelShouldThrow { get; set; }

        public Func<string, string, bool> CreateBridgeShouldThrow { get; set; }

        public Exception CreateBridgeException { get; set; }

        public List<(string BridgeId, string BridgeType)> CreatedBridges { get; } = [];

        public List<(string BridgeId, string ChannelId)> AddedChannels { get; } = [];

        public List<string> HungUpChannels { get; } = [];

        public List<string> DestroyedBridges { get; } = [];

        public List<string> OperationsLog { get; set; }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            if (OriginateShouldThrow)
            {
                throw new InvalidOperationException("Simulated originate failure.");
            }

            return Task.FromResult(OriginateChannel);
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            // Record the create BEFORE the optional throw so a test can model an ack loss: Asterisk created the bridge
            // server-side but the response was lost, so the deterministic id is live even though the caller observes
            // an exception.
            CreatedBridges.Add((bridgeId, bridgeType));

            if (CreateBridgeShouldThrow?.Invoke(bridgeId, bridgeType) == true)
            {
                throw CreateBridgeException ?? new InvalidOperationException("Simulated createBridge ack loss.");
            }

            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            AddedChannels.Add((bridgeId, channelId));

            if (AddChannelShouldThrow?.Invoke(bridgeId, channelId) == true)
            {
                throw new InvalidOperationException("Simulated addChannel failure.");
            }

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
            HungUpChannels.Add(channelId);
            OperationsLog?.Add("hangup:" + channelId);

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

        public List<string> CreatedChannelIds { get; } = [];

        public List<string> RemovedChannelIds { get; } = [];

        public List<string> OperationsLog { get; set; }

        public bool MarkConnectedResult { get; set; } = true;

        public bool MarkCallerDetachedResult { get; set; } = true;

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
            CreatedChannelIds.Add(binding.ChannelId);
            OperationsLog?.Add("create:" + binding.ChannelId);

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            RemovedChannelIds.Add(channelId);
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);
            OperationsLog?.Add("remove:" + channelId);

            return Task.CompletedTask;
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            OperationsLog?.Add("promote:" + channelId);

            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null || binding.State != AsteriskChannelBindingState.Offering)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Connected;

            return Task.FromResult(true);
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            OperationsLog?.Add("mark:" + channelId);

            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null)
            {
                return Task.FromResult(false);
            }

            if (!MarkConnectedResult)
            {
                // Model a terminal-event teardown that durably claimed the pending agent leg first: the connect flow's
                // compare-and-set loses because the record is no longer Pending. Flipping it to Terminating here makes
                // the subsequent compensation claim also lose, so only the caller is re-parked (the teardown owner
                // performs the agent-leg and mixing-bridge cleanup).
                if (binding.State == AsteriskChannelBindingState.Pending)
                {
                    binding.PreTeardownState = binding.State;
                    binding.State = AsteriskChannelBindingState.Terminating;
                }

                return Task.FromResult(false);
            }

            if (binding.State != AsteriskChannelBindingState.Pending)
            {
                return Task.FromResult(false);
            }

            binding.State = AsteriskChannelBindingState.Connected;

            return Task.FromResult(true);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            OperationsLog?.Add("mark-caller-detached:" + channelId);

            var binding = _bindings.Find(item => item.ChannelId == channelId);

            if (binding is null)
            {
                return Task.FromResult(false);
            }

            if (!MarkCallerDetachedResult)
            {
                // Model a terminal-event teardown that durably claimed the still-pending agent leg BEFORE the connect
                // flow reached the caller-detach fence. The compare-and-set loses, and flipping the record to
                // Terminating makes the later finalize and compensation claims lose too, so the connect flow must abort
                // without ever detaching the caller from holding.
                if (binding.State == AsteriskChannelBindingState.Pending)
                {
                    binding.PreTeardownState = binding.State;
                    binding.State = AsteriskChannelBindingState.Terminating;
                }

                return Task.FromResult(false);
            }

            if (binding.State != AsteriskChannelBindingState.Pending)
            {
                return Task.FromResult(false);
            }

            binding.CallerDetached = true;

            return Task.FromResult(true);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            OperationsLog?.Add("begin-teardown:" + channelId);

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
