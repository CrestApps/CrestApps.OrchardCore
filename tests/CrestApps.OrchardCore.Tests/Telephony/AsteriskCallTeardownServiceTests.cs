using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskCallTeardownServiceTests
{
    [Fact]
    public async Task ReleaseAsync_WhenAgentLegTerminates_ReleasesBridgeCallerAndBindings()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await SeedConnectedCallAsync(bindingStore);
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "StasisEnd",
                ChannelId = "agent-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("mixing-1", ariClient.DestroyedBridges);
        Assert.Contains(AsteriskConstants.HoldingBridgePrefix + "caller-1", ariClient.DestroyedBridges);
        Assert.Contains("caller-1", ariClient.HungupChannels);
        Assert.Null(await bindingStore.FindByChannelIdAsync("agent-1"));
        Assert.Null(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    [Fact]
    public async Task ReleaseAsync_WhenConnectedCallerLegTerminates_ReleasesAgentBridgeAndBindings()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await SeedConnectedCallAsync(bindingStore);
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "ChannelDestroyed",
                ChannelId = "caller-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("mixing-1", ariClient.DestroyedBridges);
        Assert.Contains(AsteriskConstants.HoldingBridgePrefix + "caller-1", ariClient.DestroyedBridges);
        Assert.Contains("agent-1", ariClient.HungupChannels);
        Assert.Null(await bindingStore.FindByChannelIdAsync("agent-1"));
        Assert.Null(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    [Fact]
    public async Task ReleaseAsync_WhenNeverConnectedCallerLegTerminates_ReleasesHoldingBridgeOnly()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "caller-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
        });
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "StasisEnd",
                ChannelId = "caller-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(AsteriskConstants.HoldingBridgePrefix + "caller-1", ariClient.DestroyedBridges);
        Assert.Empty(ariClient.HungupChannels);
        Assert.Null(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    [Fact]
    public async Task ReleaseAsync_WhenEventIsNotTerminal_DoesNothing()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await SeedConnectedCallAsync(bindingStore);
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "StasisStart",
                ChannelId = "caller-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.DestroyedBridges);
        Assert.Empty(ariClient.HungupChannels);
        Assert.NotNull(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    [Fact]
    public async Task ReleaseAsync_WhenChannelNotOwned_DoesNothing()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "ChannelDestroyed",
                ChannelId = "unknown-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.DestroyedBridges);
        Assert.Empty(ariClient.HungupChannels);
    }

    [Fact]
    public async Task ReleaseAsync_WhenSecondTerminalEventArrives_IsIdempotent()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await SeedConnectedCallAsync(bindingStore);
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "StasisEnd",
                ChannelId = "caller-1",
            },
            TestContext.Current.CancellationToken);
        ariClient.Reset();
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "ChannelDestroyed",
                ChannelId = "caller-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ariClient.DestroyedBridges);
        Assert.Empty(ariClient.HungupChannels);
    }

    [Fact]
    public async Task ReleaseAsync_WhenPendingAgentLegTerminates_TearsDownBridgeButPreservesCaller()
    {
        // Arrange
        var bindingStore = new InMemoryBindingStore();
        var ariClient = new RecordingAriClient();
        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "caller-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
        });
        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "agent-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
            BridgeId = "mixing-1",
            PeerChannelId = "caller-1",
            State = AsteriskChannelBindingState.Pending,
        });
        var service = CreateService(bindingStore, ariClient);

        // Act
        await service.ReleaseAsync(
            new AsteriskRealtimeVoiceEvent
            {
                EventType = "StasisEnd",
                ChannelId = "agent-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("mixing-1", ariClient.DestroyedBridges);
        Assert.DoesNotContain("caller-1", ariClient.HungupChannels);

        // The agent leg was still provisioning (Pending), so teardown tears down the bridge but must NOT delete the
        // durable record: a racing connect flow may still allocate or detach against it. The record is retained in the
        // Terminating state so the reconciler idempotently sweeps the deterministic agent resources and only retires
        // the claim once the provisioning lease has elapsed.
        var retained = await bindingStore.FindByChannelIdAsync("agent-1");
        Assert.NotNull(retained);
        Assert.Equal(AsteriskChannelBindingState.Terminating, retained.State);
        Assert.Equal(AsteriskChannelBindingState.Pending, retained.PreTeardownState);
        Assert.NotNull(await bindingStore.FindByChannelIdAsync("caller-1"));
    }

    private static AsteriskCallTeardownService CreateService(
        InMemoryBindingStore bindingStore,
        RecordingAriClient ariClient)
    {
        return new AsteriskCallTeardownService(
            bindingStore,
            ariClient,
            NullLogger<AsteriskCallTeardownService>.Instance);
    }

    private static async Task SeedConnectedCallAsync(InMemoryBindingStore bindingStore)
    {
        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "caller-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
        });

        await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = "agent-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = "caller-1",
            BridgeId = "mixing-1",
            PeerChannelId = "caller-1",
        });
    }

    private sealed class InMemoryBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];

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

    private sealed class RecordingAriClient : IAsteriskAriClient
    {
        public List<string> DestroyedBridges { get; } = [];

        public List<string> HungupChannels { get; } = [];

        public void Reset()
        {
            DestroyedBridges.Clear();
            HungupChannels.Clear();
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
    }
}
