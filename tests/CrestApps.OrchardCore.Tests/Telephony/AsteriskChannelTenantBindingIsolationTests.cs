using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Migrations;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
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
            var tenantABinding = await tenantABindingStore.FindByChannelIdAsync("shared-channel-1");
            var tenantBBinding = await tenantBBindingStore.FindByChannelIdAsync("shared-channel-1");
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
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1");

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
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1");

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
            var reloaded = await bindingStore.FindByChannelIdAsync("agent-channel-1");

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
            var destination = await bindingStore.FindByChannelIdAsync("transfer-agent-1");
            var previous = await bindingStore.FindByChannelIdAsync("previous-agent-1");

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
            var previous = await bindingStore.FindByChannelIdAsync("previous-agent-1");

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
            var destination = await bindingStore.FindByChannelIdAsync("transfer-agent-1");

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

    private static AsteriskChannelTenantBindingStore CreateBindingStore(IStore store, string tenantName = "Default")
    {
        return new AsteriskChannelTenantBindingStore(store, new ShellSettings { Name = tenantName });
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
