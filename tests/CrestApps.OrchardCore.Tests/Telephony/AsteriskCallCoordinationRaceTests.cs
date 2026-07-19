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

/// <summary>
/// Proves that a caller-to-agent connect finalization and a concurrent terminal-event teardown for the same call
/// are linearized by a single durable compare-and-set — not an external lock — so exactly one side wins: whichever
/// commits the binding state transition first owns the call's disposition. A terminal event can never strand the
/// caller nor let the provider report a false success, and a re-offered call is fenced from a prior attempt's
/// paused teardown because each connect attempt derives distinct ARI resource ids from its command id.
/// </summary>
public sealed class AsteriskCallCoordinationRaceTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Finalize_WhenTeardownClaimsPendingLegFirst_LosesTheCasCompensatesAndReparksCaller()
    {
        // Arrange: one shared binding store so the real connect provider and the real teardown service genuinely
        // contend on the same durable compare-and-set for the pending agent leg.
        var bindingStore = new SharedBindingStore();
        var providerAri = new BarrierProviderAriClient();
        var teardownAri = new BarrierTeardownAriClient();

        var clock = new Mock<IClock>();
        clock.SetupGet(clock => clock.UtcNow).Returns(_now);

        var provider = new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            providerAri,
            bindingStore,
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
            clock.Object,
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            new TestStringLocalizer());

        var teardown = new AsteriskCallTeardownService(
            bindingStore,
            teardownAri,
            NullLogger<AsteriskCallTeardownService>.Instance);

        // Act: start the connect flow; it persists the pending agent binding and then pauses just before finalizing,
        // while both legs are being bridged.
        var connectTask = provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = "command-1",
            },
        }, TestContext.Current.CancellationToken);

        // The connect flow derives its agent channel id deterministically from the interaction id and per-acceptance
        // command id, so the terminal-event teardown targets that exact id.
        var agentChannelId = AsteriskAriConstants.AgentChannelPrefix + "interaction-1-command-1";

        await providerAri.PreFinalizeReached;

        // The agent's terminal event fires. The teardown claims the pending binding with the durable compare-and-set
        // (transitioning it to Terminating) and only then begins its ARI effects, where it pauses inside DestroyBridge.
        // By the time this signal fires the claim is already committed, so the connect flow's later MarkConnectedAsync
        // must observe the binding as no longer promotable.
        var teardownTask = teardown.ReleaseAsync(new AsteriskRealtimeVoiceEvent
        {
            EventType = "ChannelDestroyed",
            ChannelId = agentChannelId,
        }, TestContext.Current.CancellationToken);

        await teardownAri.AriEffectsReached;

        // Release the connect flow to finalize. Its compare-and-set loses to the already-committed teardown claim, so
        // it must compensate and re-park the caller without waiting for the teardown's in-flight DestroyBridge.
        providerAri.ReleaseFinalize();

        var result = await connectTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(teardownTask.IsCompleted);

        // Let the teardown finish its ARI effects.
        teardownAri.ReleaseDestroy();
        await teardownTask;

        // Assert: the connect flow observed the claimed binding, reported the connect as failed, and re-parked the
        // caller instead of stranding it. The teardown never hung up the caller because it saw the leg was still
        // pending (the connect flow still owned the caller).
        Assert.False(result.Succeeded);
        Assert.Equal("agent_connect_lost", result.ErrorCode);
        Assert.DoesNotContain("caller-1", teardownAri.HungupChannels);
        Assert.DoesNotContain("caller-1", providerAri.HungupChannels);
        Assert.Contains(
            providerAri.AddedChannels,
            add => add.BridgeId == AsteriskConstants.HoldingBridgePrefix + "caller-1" && add.ChannelId == "caller-1");

        // The teardown claimed the still-provisioning (Pending) agent leg, so it retains the durable record in the
        // Terminating state instead of deleting it. The reconciler owns final retirement once the provisioning lease
        // elapses, which keeps the deterministic agent resources sweepable if the losing connect flow had crashed
        // mid-compensation.
        var retained = await bindingStore.FindByChannelIdAsync(agentChannelId);
        Assert.NotNull(retained);
        Assert.Equal(AsteriskChannelBindingState.Terminating, retained.State);
    }

    [Fact]
    public async Task Finalize_WhenItCommitsConnectedFirst_SucceedsAndTeardownReleasesTheConnectedCall()
    {
        // Arrange: a parked caller binding (as the inbound offer flow would have created it) plus a shared ARI client
        // and binding store, so the connect promotes the agent leg to Connected and a later terminal event tears the
        // fully connected call down.
        var bindingStore = new SharedBindingStore();
        var ariClient = new RecordingAriClient();

        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "caller-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
            State = AsteriskChannelBindingState.Connected,
        });

        var clock = new Mock<IClock>();
        clock.SetupGet(clock => clock.UtcNow).Returns(_now);

        var provider = new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore,
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
            clock.Object,
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            new TestStringLocalizer());

        var teardown = new AsteriskCallTeardownService(
            bindingStore,
            ariClient,
            NullLogger<AsteriskCallTeardownService>.Instance);

        // Act: finalize the connect. The compare-and-set commits the agent leg to Connected before any terminal event,
        // so the connect succeeds.
        var result = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = "command-1",
            },
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var agentChannelId = result.Metadata[AsteriskAriConstants.AgentChannelMetadataKey];
        var bridgeId = result.Metadata["bridgeId"];

        var connectedBinding = await bindingStore.FindByChannelIdAsync(agentChannelId);
        Assert.NotNull(connectedBinding);
        Assert.Equal(AsteriskChannelBindingState.Connected, connectedBinding.State);

        // The agent leg then terminates. Because the leg was already Connected, the teardown claims it and its peer,
        // releasing the caller and tearing down both the mixing and holding bridges.
        await teardown.ReleaseAsync(new AsteriskRealtimeVoiceEvent
        {
            EventType = "ChannelDestroyed",
            ChannelId = agentChannelId,
        }, TestContext.Current.CancellationToken);

        // Assert: the fully connected call was released end to end and no binding leaked.
        Assert.Contains(bridgeId, ariClient.DestroyedBridges);
        Assert.Contains(AsteriskConstants.HoldingBridgePrefix + "caller-1", ariClient.DestroyedBridges);
        Assert.Contains("caller-1", ariClient.HungupChannels);
        Assert.Null(await bindingStore.FindByChannelIdAsync(agentChannelId));
        Assert.Null(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    [Fact]
    public async Task Connect_WhenReofferedAsANewCommand_DerivesDistinctAriResourcesToFenceAba()
    {
        // Arrange: a shared ARI client that echoes each originate's deterministic channel id, so every connect ATTEMPT
        // materializes its own agent channel and bridge exactly as production would.
        var bindingStore = new SharedBindingStore();
        var ariClient = new RecordingAriClient(echoOriginateChannelId: true);

        var clock = new Mock<IClock>();
        clock.SetupGet(clock => clock.UtcNow).Returns(_now);

        var provider = new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore,
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
            clock.Object,
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            new TestStringLocalizer());

        // Act: two connect attempts for the SAME interaction and caller, differing only by their per-acceptance
        // command id — exactly the re-offer scenario where a prior attempt's teardown could still be paused.
        var first = await provider.ConnectToAgentAsync(CreateReofferRequest("command-1"), TestContext.Current.CancellationToken);
        var second = await provider.ConnectToAgentAsync(CreateReofferRequest("command-2"), TestContext.Current.CancellationToken);

        // Assert: both attempts succeed but derive DISTINCT bridge and agent channel ids, so a stale teardown from the
        // first attempt can only ever destroy the first generation's resources, never the re-offered call's.
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Metadata["bridgeId"], second.Metadata["bridgeId"]);
        Assert.NotEqual(
            first.Metadata[AsteriskAriConstants.AgentChannelMetadataKey],
            second.Metadata[AsteriskAriConstants.AgentChannelMetadataKey]);
    }

    private static ContactCenterConnectRequest CreateReofferRequest(string commandId)
    {
        return new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = commandId,
            },
        };
    }

    private sealed class SharedBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly object _gate = new();
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(_bindings.ToArray());
            }
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_bindings.Count > 0);
            }
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
        {
            lock (_gate)
            {
                return Task.FromResult(_bindings.Find(binding => binding.ChannelId == channelId));
            }
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(
                    _bindings.Where(binding => binding.PeerChannelId == peerChannelId).ToArray());
            }
        }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            lock (_gate)
            {
                _bindings.Add(binding);
            }

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            lock (_gate)
            {
                _bindings.RemoveAll(binding => binding.ChannelId == channelId);
            }

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            lock (_gate)
            {
                var binding = _bindings.Find(item => item.ChannelId == channelId);

                if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
                {
                    return Task.FromResult(false);
                }

                binding.State = AsteriskChannelBindingState.Connected;

                return Task.FromResult(true);
            }
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            lock (_gate)
            {
                var binding = _bindings.Find(item => item.ChannelId == channelId);

                if (binding is null || binding.State != AsteriskChannelBindingState.Offering)
                {
                    return Task.FromResult(false);
                }

                binding.State = AsteriskChannelBindingState.Connected;

                return Task.FromResult(true);
            }
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            lock (_gate)
            {
                var binding = _bindings.Find(item => item.ChannelId == channelId);

                if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
                {
                    return Task.FromResult(false);
                }

                binding.CallerDetached = true;

                return Task.FromResult(true);
            }
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            lock (_gate)
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

    private sealed class BarrierProviderAriClient : IAsteriskAriClient
    {
        private readonly TaskCompletionSource _preFinalize = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFinalize = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _finalizeGateArmed = true;

        public List<(string BridgeId, string ChannelId)> AddedChannels { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public Task PreFinalizeReached => _preFinalize.Task;

        public void ReleaseFinalize()
        {
            _releaseFinalize.TrySetResult();
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(channelId == "caller-1");
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            return Task.FromResult<AsteriskAriBridge>(null);
        }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel
            {
                Id = "agent-chan-1",
            });
        }

        public async Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            AddedChannels.Add((bridgeId, channelId));

            if (channelId.StartsWith(AsteriskAriConstants.AgentChannelPrefix, StringComparison.Ordinal) && _finalizeGateArmed)
            {
                _finalizeGateArmed = false;
                _preFinalize.TrySetResult();

                await _releaseFinalize.Task;
            }
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannels.Add(channelId);

            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
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
    }

    private sealed class BarrierTeardownAriClient : IAsteriskAriClient
    {
        private readonly TaskCompletionSource _ariEffectsReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseDestroy = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _destroyGateArmed = true;

        public List<string> DestroyedBridges { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public Task AriEffectsReached => _ariEffectsReached.Task;

        public void ReleaseDestroy()
        {
            _releaseDestroy.TrySetResult();
        }

        public async Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            DestroyedBridges.Add(bridgeId);

            if (_destroyGateArmed)
            {
                _destroyGateArmed = false;
                _ariEffectsReached.TrySetResult();

                await _releaseDestroy.Task;
            }
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannels.Add(channelId);

            return Task.CompletedTask;
        }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<AsteriskAriChannel>(null);
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            return Task.FromResult<AsteriskAriBridge>(null);
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
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

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
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
    }

    private sealed class RecordingAriClient : IAsteriskAriClient
    {
        private readonly bool _echoOriginateChannelId;

        public RecordingAriClient(bool echoOriginateChannelId = false)
        {
            _echoOriginateChannelId = echoOriginateChannelId;
        }

        public List<string> CreatedBridges { get; } = [];

        public List<(string BridgeId, string ChannelId)> AddedChannels { get; } = [];

        public List<string> DestroyedBridges { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(channelId == "caller-1");
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            CreatedBridges.Add(bridgeId);

            return Task.FromResult(new AsteriskAriBridge
            {
                Id = bridgeId,
            });
        }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel
            {
                Id = _echoOriginateChannelId ? request.ChannelId : "agent-chan-1",
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

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            DestroyedBridges.Add(bridgeId);

            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            HungupChannels.Add(channelId);

            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
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
}
