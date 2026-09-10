using System.Reflection;
using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Migrations;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskChannelTenantBindingIsolationTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IsOwnedByCurrentTenantAsync_WhenSameChannelExistsInTenantA_IsInvisibleToTenantB()
    {
        // Arrange
        var tenantAPath = DatabasePath("tenant-a");
        var tenantBPath = DatabasePath("tenant-b");
        var tenantAStore = await CreateStoreAsync(tenantAPath);
        var tenantBStore = await CreateStoreAsync(tenantBPath);

        try
        {
            var tenantABindingStore = CreateBindingStore(tenantAStore, "TenantA");
            var tenantBBindingStore = CreateBindingStore(tenantBStore, "TenantB");
            await tenantABindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "shared-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "shared-channel-1",
                CreatedUtc = _now,
            });

            var tenantAGuard = new AsteriskChannelOwnershipGuard(tenantABindingStore);
            var tenantBGuard = new AsteriskChannelOwnershipGuard(tenantBBindingStore);

            // Act
            var tenantAOwned = await tenantAGuard.IsOwnedByCurrentTenantAsync("shared-channel-1");
            var tenantBOwned = await tenantBGuard.IsOwnedByCurrentTenantAsync("shared-channel-1");
            var tenantABinding = await tenantABindingStore.FindByChannelIdAsync("shared-channel-1", TestContext.Current.CancellationToken);
            var tenantBBinding = await tenantBBindingStore.FindByChannelIdAsync("shared-channel-1", TestContext.Current.CancellationToken);
            var tenantABindings = await tenantABindingStore.GetAllAsync(TestContext.Current.CancellationToken);
            var tenantBBindings = await tenantBBindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(tenantAOwned);
            Assert.NotNull(tenantABinding);
            Assert.Single(tenantABindings);
            Assert.False(tenantBOwned);
            Assert.Null(tenantBBinding);
            Assert.Empty(tenantBBindings);
        }
        finally
        {
            tenantAStore.Dispose();
            tenantBStore.Dispose();
            TryDelete(tenantAPath);
            TryDelete(tenantBPath);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenChannelAlreadyExistsInTenant_DoesNotDuplicateBinding()
    {
        // Arrange
        var databasePath = DatabasePath("idempotent");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            var firstCreated = await bindingStore.CreateAsync(CreateBinding("shared-channel-1"));

            // Act
            var secondCreated = await bindingStore.CreateAsync(CreateBinding("shared-channel-1"));

            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(firstCreated);
            Assert.False(secondCreated);
            Assert.Single(bindings);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenManyConcurrentCallsRaceTheSameChannel_CreatesExactlyOneBindingAndReturnsCreatedOnce()
    {
        // Arrange
        var databasePath = DatabasePath("concurrent-create");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // Act
            var racers = Enumerable
                .Range(0, 32)
                .Select(_ => bindingStore.CreateAsync(CreateBinding("shared-channel-1")))
                .ToArray();

            var results = await Task.WhenAll(racers);

            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(bindings);
            Assert.Equal(1, results.Count(created => created));
            Assert.Equal(31, results.Count(created => !created));
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task MarkConnectedAsync_PromotesPendingBindingDurablyAcrossSessions()
    {
        // Arrange
        var databasePath = DatabasePath("mark-connected");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now,
            });

            // Act
            var marked = await bindingStore.MarkConnectedAsync("agent-channel-1");
            var missing = await bindingStore.MarkConnectedAsync("unknown-channel");
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.True(marked);
            Assert.False(missing);
            Assert.NotNull(reloaded);
            Assert.Equal(AsteriskChannelBindingState.Connected, reloaded.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryBeginTeardownAsync_ClaimsBindingOnceAndPersistsTerminatingStateDurably()
    {
        // Arrange
        var databasePath = DatabasePath("begin-teardown");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now,
            });

            // Act
            var firstClaim = await bindingStore.TryBeginTeardownAsync("agent-channel-1");
            var secondClaim = await bindingStore.TryBeginTeardownAsync("agent-channel-1");
            var missingClaim = await bindingStore.TryBeginTeardownAsync("unknown-channel");
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(firstClaim);
            Assert.Equal(AsteriskChannelBindingState.Pending, firstClaim.PreviousState);
            Assert.Equal("agent-channel-1", firstClaim.Binding.ChannelId);
            Assert.Null(secondClaim);
            Assert.Null(missingClaim);
            Assert.NotNull(reloaded);
            Assert.Equal(AsteriskChannelBindingState.Terminating, reloaded.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task MarkConnectedAsync_WhenBindingAlreadyClaimedForTeardown_LosesTheCas()
    {
        // Arrange
        var databasePath = DatabasePath("finalize-loses");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "agent-channel-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _now,
            });

            // Act
            var claim = await bindingStore.TryBeginTeardownAsync("agent-channel-1");
            var connected = await bindingStore.MarkConnectedAsync("agent-channel-1");
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(claim);
            Assert.False(connected);
            Assert.NotNull(reloaded);
            Assert.Equal(AsteriskChannelBindingState.Terminating, reloaded.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task SwapConnectedOwnerAsync_WhenDestinationIsJoining_PromotesItAndRetiresPreviousInOneTransaction()
    {
        // Arrange
        var databasePath = DatabasePath("swap-commits");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "previous-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "transfer-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            var swapped = await bindingStore.SwapConnectedOwnerAsync("transfer-agent-1", "previous-agent-1");
            var destination = await bindingStore.FindByChannelIdAsync("transfer-agent-1", TestContext.Current.CancellationToken);
            var previous = await bindingStore.FindByChannelIdAsync("previous-agent-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.True(swapped);
            Assert.NotNull(destination);
            Assert.Equal(AsteriskChannelBindingState.Connected, destination.State);

            // The previous owner is retired to a non-owning Terminating (Joining-disposition) recovery record — never
            // an id-only delete — so the transition is the version-checked linearization point and the previous leg
            // keeps a durable record until its hangup is confirmed.
            Assert.NotNull(previous);
            Assert.Equal(AsteriskChannelBindingState.Terminating, previous.State);
            Assert.Equal(AsteriskChannelBindingState.Joining, previous.PreTeardownState);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task SwapConnectedOwnerAsync_WhenDestinationNoLongerJoining_DoesNotCommitAndKeepsPreviousOwner()
    {
        // Arrange
        var databasePath = DatabasePath("swap-loses");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "previous-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });

            // A terminal event already claimed the destination leg for teardown, so it is no longer Joining.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "transfer-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            var swapped = await bindingStore.SwapConnectedOwnerAsync("transfer-agent-1", "previous-agent-1");
            var previous = await bindingStore.FindByChannelIdAsync("previous-agent-1", TestContext.Current.CancellationToken);

            // Assert
            // The swap must not commit, and the previous agent leg must remain the Connected owner so the customer
            // safely keeps the previous agent.
            Assert.False(swapped);
            Assert.NotNull(previous);
            Assert.Equal(AsteriskChannelBindingState.Connected, previous.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task SwapConnectedOwnerAsync_WhenPreviousOwnerAlreadySwappedAway_DoesNotPromoteASecondConnectedOwner()
    {
        // Arrange
        var databasePath = DatabasePath("swap-guards-second-owner");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // The previous agent leg's binding no longer exists as a Connected owner (a prior transfer already swapped
            // ownership away, or the previous agent's own terminal event claimed it).
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "transfer-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            var swapped = await bindingStore.SwapConnectedOwnerAsync("transfer-agent-1", "previous-agent-1");
            var destination = await bindingStore.FindByChannelIdAsync("transfer-agent-1", TestContext.Current.CancellationToken);

            // Assert
            // Promoting a destination whose previous owner is gone would create a SECOND Connected owner of the shared
            // bridge (a double-teardown drop). The swap must reject and leave the destination a non-owning Joining leg
            // for the reconciler to reclaim.
            Assert.False(swapped);
            Assert.NotNull(destination);
            Assert.Equal(AsteriskChannelBindingState.Joining, destination.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task SwapConnectedOwnerAsync_WhenTwoTransfersRaceTheSamePreviousOwner_PromotesExactlyOneConnectedOwner()
    {
        // Arrange
        var databasePath = DatabasePath("swap-concurrent-owner");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // One conversation, one current Connected owner, and TWO distinct destination legs that both raced to
            // transfer the same call to different agents. Each destination has its own Joining claim.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "previous-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "transfer-agent-a",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "transfer-agent-b",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            // Both transfers finalize concurrently, contending on the SAME previous owner. Because retiring the
            // previous owner is a version-checked transition (not an id-only delete), only one swap can move it out of
            // Connected; the other must lose and reject.
            var swapA = bindingStore.SwapConnectedOwnerAsync("transfer-agent-a", "previous-agent-1");
            var swapB = bindingStore.SwapConnectedOwnerAsync("transfer-agent-b", "previous-agent-1");
            var results = await Task.WhenAll(swapA, swapB);

            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            // Exactly one swap commits, and the canonical bridge ends with EXACTLY ONE Connected owner — never two.
            Assert.Equal(1, results.Count(swapped => swapped));
            Assert.Single(bindings, binding => binding.State == AsteriskChannelBindingState.Connected);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryHandOffBridgeOwnershipAsync_WhenOwnerDepartsWithParticipant_PromotesParticipantAndRetiresOwnerAtomically()
    {
        // Arrange
        var databasePath = DatabasePath("handoff-promote");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // A departing owner already claimed to Terminating (with a Connected pre-teardown disposition) and a
            // remaining non-owning Participating leg on the same canonical bridge.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "owner-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });

            // Act
            var handedOff = await bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");
            var promoted = await bindingStore.FindByChannelIdAsync("participant-2", TestContext.Current.CancellationToken);
            var owner = await bindingStore.FindByChannelIdAsync("owner-1", TestContext.Current.CancellationToken);

            // Assert
            // The participant is promoted to the sole Connected owner AND the departing owner is atomically retired to a
            // non-owning Joining disposition so no later sweep can re-process it as a Connected owner.
            Assert.True(handedOff);
            Assert.NotNull(promoted);
            Assert.Equal(AsteriskChannelBindingState.Connected, promoted.State);
            Assert.NotNull(owner);
            Assert.Equal(AsteriskChannelBindingState.Terminating, owner.State);
            Assert.Equal(AsteriskChannelBindingState.Joining, owner.PreTeardownState);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryHandOffBridgeOwnershipAsync_WhenReprocessingAnAlreadyRetiredOwner_DoesNotPromoteASecondOwner()
    {
        // Arrange
        var databasePath = DatabasePath("handoff-reprocess");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // A departing owner and TWO remaining participants: after the first hand-off retires the owner and promotes
            // one participant, the owner's record survives (its later id-only removal has not yet landed, or crashed).
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "owner-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-3",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });

            // Act
            // The first sweep hands the bridge off. A second sweep re-processes the SAME retired owner record (the exact
            // window where a crash left the owner record behind after promotion).
            var firstHandOff = await bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");
            var secondHandOff = await bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");

            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            // Both calls report the bridge as retained (never destroy it), and re-processing the retired owner must NOT
            // promote the second participant into a SECOND Connected owner — the single-owner-destroyer invariant holds.
            Assert.True(firstHandOff);
            Assert.True(secondHandOff);
            Assert.Single(bindings, binding => binding.State == AsteriskChannelBindingState.Connected);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryHandOffBridgeOwnershipAsync_WhenNoParticipantRemains_ReturnsFalseAndLeavesOwnerForDestroy()
    {
        // Arrange
        var databasePath = DatabasePath("handoff-last-owner");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "owner-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });

            // Act
            var handedOff = await bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");
            var owner = await bindingStore.FindByChannelIdAsync("owner-1", TestContext.Current.CancellationToken);

            // Assert
            // With no successor, the departing owner must destroy the bridge and release the caller, so the hand-off
            // reports false and the owner's Connected disposition is left untouched for the destroy path.
            Assert.False(handedOff);
            Assert.NotNull(owner);
            Assert.Equal(AsteriskChannelBindingState.Connected, owner.PreTeardownState);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryHandOffBridgeOwnershipAsync_WhenTwoOwnerDeparturesRaceTheSameBridge_PromotesExactlyOneConnectedOwner()
    {
        // Arrange
        var databasePath = DatabasePath("handoff-concurrent");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // One departing owner claimed to Terminating and two remaining participants. A live teardown and a
            // reconciler sweep can both process the same claimed owner concurrently.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "owner-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-3",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });

            // Act
            // Both hand-offs contend on the SAME owner's document version. Because retiring the owner is version-checked
            // and atomic with the promotion, only one can commit; the loser re-reads the retired owner and returns
            // without promoting a second participant.
            var handOffA = bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");
            var handOffB = bindingStore.TryHandOffBridgeOwnershipAsync("mixing-1", "owner-1");
            var results = await Task.WhenAll(handOffA, handOffB);

            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            // Both report the bridge retained, and the canonical bridge ends with EXACTLY ONE Connected owner — never two.
            Assert.True(results[0]);
            Assert.True(results[1]);
            Assert.Single(bindings, binding => binding.State == AsteriskChannelBindingState.Connected);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryPromoteJoiningToParticipatingAsync_WhenLegIsJoining_PromotesToParticipating()
    {
        // Arrange
        var databasePath = DatabasePath("promote-participating");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            var promoted = await bindingStore.TryPromoteJoiningToParticipatingAsync("participant-2");
            var binding = await bindingStore.FindByChannelIdAsync("participant-2", TestContext.Current.CancellationToken);

            // Assert
            Assert.True(promoted);
            Assert.NotNull(binding);
            Assert.Equal(AsteriskChannelBindingState.Participating, binding.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryPromoteJoiningToParticipatingAsync_WhenLegIsNotJoining_RejectsPromotion()
    {
        // Arrange
        var databasePath = DatabasePath("promote-participating-reject");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // A leg a terminal event already claimed for teardown must never be treated as a live participant.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "participant-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Terminating,
                PreTeardownState = AsteriskChannelBindingState.Joining,
                CreatedUtc = _now,
            });

            // Act
            var promoted = await bindingStore.TryPromoteJoiningToParticipatingAsync("participant-2");
            var binding = await bindingStore.FindByChannelIdAsync("participant-2", TestContext.Current.CancellationToken);

            // Assert
            Assert.False(promoted);
            Assert.NotNull(binding);
            Assert.Equal(AsteriskChannelBindingState.Terminating, binding.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimProvisionalLegForTeardownAsync_WhenLegIsParticipating_ClaimsItForTeardown()
    {
        // Arrange
        var databasePath = DatabasePath("claim-provisional-participating");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "consult-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });

            // Act
            var claimed = await bindingStore.TryClaimProvisionalLegForTeardownAsync("consult-2");
            var binding = await bindingStore.FindByChannelIdAsync("consult-2", TestContext.Current.CancellationToken);

            // Assert
            Assert.True(claimed);
            Assert.NotNull(binding);
            Assert.Equal(AsteriskChannelBindingState.Terminating, binding.State);
            Assert.Equal(AsteriskChannelBindingState.Participating, binding.PreTeardownState);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimProvisionalLegForTeardownAsync_WhenLegIsConnectedOwner_RejectsAndLeavesItConnected()
    {
        // Arrange
        var databasePath = DatabasePath("claim-provisional-connected");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // The consult leg was already promoted in place to the sole Connected owner by a completed transfer, so a
            // stale cancel must NEVER be able to claim (and therefore hang up) it — doing so would drop the customer.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "consult-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });

            // Act
            var claimed = await bindingStore.TryClaimProvisionalLegForTeardownAsync("consult-2");
            var binding = await bindingStore.FindByChannelIdAsync("consult-2", TestContext.Current.CancellationToken);

            // Assert
            Assert.False(claimed);
            Assert.NotNull(binding);
            Assert.Equal(AsteriskChannelBindingState.Connected, binding.State);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimProvisionalLegForTeardownAsync_WhenClaimRacesConsultCompletion_ExactlyOneWinsAndNoOwnerIsDropped()
    {
        // Arrange
        var databasePath = DatabasePath("claim-provisional-race");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            var bindingStore = CreateBindingStore(store);

            // A stabilized consult: destination B is a Participating leg on the shared bridge; the initiating agent A is
            // the current Connected owner.
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "initiating-agent-1",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Connected,
                CreatedUtc = _now,
            });
            await bindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = "consult-2",
                ProviderName = "Asterisk",
                ProviderCallId = "caller-1",
                BridgeId = "mixing-1",
                PeerChannelId = "caller-1",
                State = AsteriskChannelBindingState.Participating,
                CreatedUtc = _now,
            });

            // Act
            // A cancel (claim the consult leg for teardown) and a complete (promote the consult leg to Connected owner)
            // race on the SAME Participating binding. Both are version-checked transitions off Participating, so exactly
            // one can win: either the claim moves it to Terminating (completion then fails and A stays owner), or the
            // completion promotes it to Connected (the claim then fails and the live owner is never hung up).
            var claim = bindingStore.TryClaimProvisionalLegForTeardownAsync("consult-2");
            var complete = bindingStore.PromoteParticipantToConnectedOwnerAsync("consult-2", "initiating-agent-1");
            var claimed = await claim;
            var completed = await complete;

            var consult = await bindingStore.FindByChannelIdAsync("consult-2", TestContext.Current.CancellationToken);
            var bindings = await bindingStore.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            // The two mutually exclusive outcomes are the only safe ones; they can never both succeed.
            Assert.NotEqual(claimed, completed);
            Assert.NotNull(consult);

            if (completed)
            {
                // Completion won: the consult leg is now the live Connected owner and was NOT claimed for teardown.
                Assert.False(claimed);
                Assert.Equal(AsteriskChannelBindingState.Connected, consult.State);
                Assert.Single(bindings, binding => binding.State == AsteriskChannelBindingState.Connected);
            }
            else
            {
                // Cancel won: the consult leg is retired for teardown and the initiating agent remains the owner.
                Assert.True(claimed);
                Assert.Equal(AsteriskChannelBindingState.Terminating, consult.State);

                var initiating = await bindingStore.FindByChannelIdAsync("initiating-agent-1", TestContext.Current.CancellationToken);
                Assert.NotNull(initiating);
                Assert.Equal(AsteriskChannelBindingState.Connected, initiating.State);
            }
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenCreateLockIsHeldBeyondTheBoundedWindow_ThrowsTimeoutAndDoesNotStrandTheChannel()
    {
        // Arrange
        var databasePath = DatabasePath("create-lock-timeout");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            const string tenantName = "Default";
            const string channelId = "wedged-channel-1";

            // Occupy the exact per-channel stripe the store hashes this channel to, simulating a create that has
            // wedged on a stalled database operation while still holding the lock.
            var stripeLock = GetCreateLockForTest(tenantName, channelId);
            await stripeLock.WaitAsync(TestContext.Current.CancellationToken);

            try
            {
                var bindingStore = CreateBindingStore(store, tenantName, TimeSpan.FromMilliseconds(150));

                // Act
                var exception = await Assert.ThrowsAsync<AsteriskChannelBindingCreateTimeoutException>(
                    () => bindingStore.CreateAsync(CreateBinding(channelId)));

                // Assert
                // A timeout is a distinct, ambiguous outcome — not the false "lost the create race" flag — so the
                // caller reconciles instead of being told another attempt owns the channel and stranding it.
                Assert.Equal(channelId, exception.ChannelId);

                var wedged = await bindingStore.FindByChannelIdAsync(channelId, TestContext.Current.CancellationToken);
                Assert.Null(wedged);
            }
            finally
            {
                stripeLock.Release();
            }

            // Once the wedged holder releases the stripe, the channel can be claimed normally.
            var recoveredStore = CreateBindingStore(store, tenantName);
            var created = await recoveredStore.CreateAsync(CreateBinding(channelId));

            Assert.True(created);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimChannelForTerminationAsync_WhenNoBindingExists_ClaimsAndBlocksCreateUntilReleased()
    {
        // Arrange
        var databasePath = DatabasePath("termination-claim");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            const string channelId = "stranded-channel-1";
            var bindingStore = CreateBindingStore(store, "ClaimTenantA");

            // Act
            var claimed = await bindingStore.TryClaimChannelForTerminationAsync(channelId, TestContext.Current.CancellationToken);

            // A create for a channel that is being terminated must lose, so no delivery can route the caller into a
            // live call that the pending hang up would then tear down.
            var createdWhileClaimed = await bindingStore.CreateAsync(CreateBinding(channelId));

            bindingStore.ReleaseTerminationClaim(channelId);

            // Once the claim is released the channel can be created normally again.
            var createdAfterRelease = await bindingStore.CreateAsync(CreateBinding(channelId));

            // Assert
            Assert.True(claimed);
            Assert.False(createdWhileClaimed);
            Assert.True(createdAfterRelease);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimChannelForTerminationAsync_WhenABindingAlreadyExists_RefusesTheClaimToProtectTheRecoveredCall()
    {
        // Arrange
        var databasePath = DatabasePath("termination-claim-recovered");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            const string channelId = "recovered-channel-1";
            var bindingStore = CreateBindingStore(store, "ClaimTenantB");

            // A different delivery already claimed, answered, and routed this caller into a live, owned call.
            await bindingStore.CreateAsync(CreateBinding(channelId));

            // Act
            var claimed = await bindingStore.TryClaimChannelForTerminationAsync(channelId, TestContext.Current.CancellationToken);

            // Assert
            // The claim is refused because a binding exists, so the live, recovered call is never selected for a hang up.
            Assert.False(claimed);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TryClaimChannelForTerminationAsync_WhenTheStripeIsWedgedBeyondTheBoundedWindow_ThrowsTimeout()
    {
        // Arrange
        var databasePath = DatabasePath("termination-claim-wedged");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            const string tenantName = "ClaimTenantC";
            const string channelId = "wedged-claim-channel-1";

            // Occupy the exact per-channel stripe so the claim cannot be planted within its bounded window; the
            // termination path must surface the ambiguous timeout (which its caller defers to the reconciler) rather
            // than block on the wedged stripe indefinitely.
            var stripeLock = GetCreateLockForTest(tenantName, channelId);
            await stripeLock.WaitAsync(TestContext.Current.CancellationToken);

            try
            {
                var bindingStore = CreateBindingStore(store, tenantName, TimeSpan.FromMilliseconds(150));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<AsteriskChannelBindingCreateTimeoutException>(
                    () => bindingStore.TryClaimChannelForTerminationAsync(channelId, TestContext.Current.CancellationToken));

                Assert.Equal(channelId, exception.ChannelId);
            }
            finally
            {
                stripeLock.Release();
            }
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task TerminationClaim_SurvivesAShellReload_AndKeepsANewGenerationFencedUntilTheTerminationCompletes()
    {
        // Arrange
        var databasePath = DatabasePath("termination-claim-fence");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            const string tenantName = "ClaimTenantD";
            const string channelId = "lifecycle-channel-1";

            // The old shell generation claims the channel for termination. Its remote hang up may still be in flight
            // when the shell reloads, so the fence MUST outlive the generation that planted it.
            var oldGeneration = new AsteriskPendingCallerTerminationRegistry(new ShellSettings { Name = tenantName });
            var claimingStore = CreateBindingStore(store, tenantName, terminationRegistry: oldGeneration);

            var claimed = await claimingStore.TryClaimChannelForTerminationAsync(channelId, TestContext.Current.CancellationToken);

            // A new shell generation resolves a fresh registry instance. Because the claim is process-wide it is still
            // visible, so a create for the same channel remains fenced — the old generation's late hang up can never
            // terminate a call this generation might otherwise route.
            var newGeneration = new AsteriskPendingCallerTerminationRegistry(new ShellSettings { Name = tenantName });
            var reloadedStore = CreateBindingStore(store, tenantName, terminationRegistry: newGeneration);

            var createFencedAfterReload = await reloadedStore.CreateAsync(CreateBinding(channelId));

            // The termination completes (the caller is confirmed gone) and the claim is released. Only now may the
            // channel be created again — and because the release path is process-wide too, no claim is ever orphaned.
            reloadedStore.ReleaseTerminationClaim(channelId);

            var createPermittedAfterRelease = await reloadedStore.CreateAsync(CreateBinding(channelId));

            // Assert
            Assert.True(claimed);
            Assert.False(createFencedAfterReload);
            Assert.True(createPermittedAfterRelease);
        }
        finally
        {
            store.Dispose();
            TryDelete(databasePath);
        }
    }

    private static AsteriskChannelTenantBindingStore CreateBindingStore(
        IStore store,
        string tenantName = "Default",
        TimeSpan? createLockTimeout = null,
        IAsteriskPendingCallerTerminationRegistry terminationRegistry = null)
    {
        var options = Options.Create(new AsteriskCoordinationOptions
        {
            ChannelBindingCreateLockTimeout = createLockTimeout ?? TimeSpan.FromSeconds(10),
        });

        return new AsteriskChannelTenantBindingStore(
            store,
            new ShellSettings { Name = tenantName },
            terminationRegistry ?? new AsteriskPendingCallerTerminationRegistry(new ShellSettings { Name = tenantName }),
            options);
    }

    private static SemaphoreSlim GetCreateLockForTest(string tenantName, string channelId)
    {
        var method = typeof(AsteriskChannelTenantBindingStore).GetMethod(
            "GetCreateLock",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (SemaphoreSlim)method.Invoke(null, [tenantName, channelId]);
    }

    private static AsteriskChannelTenantBinding CreateBinding(string channelId)
    {
        return new AsteriskChannelTenantBinding
        {
            ChannelId = channelId,
            ProviderName = "Asterisk",
            ProviderCallId = channelId,
            CreatedUtc = _now,
        };
    }

    private static string DatabasePath(string prefix)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "TestArtifacts");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"asterisk-binding-{prefix}-{Guid.NewGuid():N}.db");
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AsteriskChannelTenantBindingIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new AsteriskChannelTenantBindingMigrations
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
