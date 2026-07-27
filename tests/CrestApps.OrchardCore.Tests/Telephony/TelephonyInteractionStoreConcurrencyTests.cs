using System.Data.Common;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Proves, against a real database, that two writers racing on the same telephony interaction cannot
/// silently discard each other's work.
/// </summary>
public sealed class TelephonyInteractionStoreConcurrencyTests
{
    private static readonly DateTime _startedUtc = new(2026, 8, 3, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_WhenAnotherWriterCommittedFirst_FailsInsteadOfOverwritingTheWinner()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-interaction-cas");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var sessionA = store.CreateSession();
            await using var sessionB = store.CreateSession();
            var storeA = new DefaultTelephonyInteractionStore(sessionA, store);
            var storeB = new DefaultTelephonyInteractionStore(sessionB, store);

            var fromA = await storeA.FindByCallIdAsync("user-1", "call-1", TestContext.Current.CancellationToken);
            var fromB = await storeB.FindByCallIdAsync("user-1", "call-1", TestContext.Current.CancellationToken);

            // Act
            fromA.Outcome = CallOutcome.Completed;
            fromA.EndedUtc = _startedUtc.AddMinutes(2);
            await storeA.UpdateAsync(fromA, TestContext.Current.CancellationToken);
            await sessionA.SaveChangesAsync(TestContext.Current.CancellationToken);

            fromB.Outcome = CallOutcome.Missed;
            await storeB.UpdateAsync(fromB, TestContext.Current.CancellationToken);
            var exception = await Record.ExceptionAsync(() => sessionB.SaveChangesAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.True(
                exception is ConcurrencyException or DbException,
                $"Expected an optimistic-concurrency failure but received {exception?.GetType().Name ?? "no exception"}.");

            var persisted = await ReadAsync(store, "interaction-1");
            Assert.Equal(CallOutcome.Completed, persisted.Outcome);
            Assert.Equal(_startedUtc.AddMinutes(2), persisted.EndedUtc);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UpdateByIdAsync_WhenAWriterCommitsBetweenTheReadAndTheWrite_KeepsBothUpdates()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-interaction-retry");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var session = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(session, store);
            var attempts = 0;

            // Act
            // The first attempt reads the seeded version, then a competing writer commits underneath it. Without a
            // re-read the competing writer's field would be silently reverted by the mutation below.
            var updated = await interactionStore.UpdateByIdAsync(
                "interaction-1",
                candidate =>
                {
                    attempts++;

                    if (attempts == 1)
                    {
                        CommitCompetingWriteAsync(store).GetAwaiter().GetResult();
                    }

                    candidate.Outcome = CallOutcome.Completed;
                    candidate.EndedUtc = _startedUtc.AddMinutes(3);

                    return true;
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, attempts);
            Assert.NotNull(updated);

            var persisted = await ReadAsync(store, "interaction-1");
            Assert.Equal(CallOutcome.Completed, persisted.Outcome);
            Assert.Equal(_startedUtc.AddMinutes(3), persisted.EndedUtc);
            Assert.Equal("competing-writer", persisted.UserName);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UpdateByProviderCallIdAsync_WhenAWriterCommitsBetweenTheReadAndTheWrite_ReevaluatesTheTerminalGuard()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-interaction-terminal-guard");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var session = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(session, store);
            var attempts = 0;
            var observedOutcomes = new List<CallOutcome>();

            // Act
            // A hangup commits after the first read. The retry must observe the terminal outcome so that the
            // in-flight mutation declines rather than resurrecting a call that already ended.
            var updated = await interactionStore.UpdateByProviderCallIdAsync(
                "ProviderA",
                "call-1",
                candidate =>
                {
                    attempts++;
                    observedOutcomes.Add(candidate.Outcome);

                    if (candidate.Outcome != CallOutcome.InProgress)
                    {
                        return false;
                    }

                    if (attempts == 1)
                    {
                        CommitTerminalWriteAsync(store).GetAwaiter().GetResult();
                    }

                    candidate.UserName = "resurrected";

                    return true;
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, attempts);
            Assert.Equal([CallOutcome.InProgress, CallOutcome.Completed], observedOutcomes);
            Assert.NotNull(updated);

            var persisted = await ReadAsync(store, "interaction-1");
            Assert.Equal(CallOutcome.Completed, persisted.Outcome);
            Assert.NotEqual("resurrected", persisted.UserName);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UpdateByIdAsync_WhenTheMutationDeclines_LeavesThePersistedInteractionUntouched()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-interaction-decline");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var session = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(session, store);

            // Act
            var updated = await interactionStore.UpdateByIdAsync(
                "interaction-1",
                candidate =>
                {
                    candidate.Outcome = CallOutcome.Missed;

                    return false;
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(updated);

            var persisted = await ReadAsync(store, "interaction-1");
            Assert.Equal(CallOutcome.InProgress, persisted.Outcome);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UpdateByIdAsync_WhenNoInteractionMatches_ReturnsNullWithoutWriting()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-interaction-missing");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var session = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(session, store);
            var invoked = false;

            // Act
            var updated = await interactionStore.UpdateByIdAsync(
                "interaction-missing",
                candidate =>
                {
                    invoked = true;

                    return true;
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(updated);
            Assert.False(invoked);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task CommitCompetingWriteAsync(IStore store)
    {
        await using var session = store.CreateSession();

        var interaction = await session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.InteractionId == "interaction-1")
            .FirstOrDefaultAsync();

        interaction.UserName = "competing-writer";
        await session.SaveAsync(interaction, checkConcurrency: true);
        await session.SaveChangesAsync();
    }

    private static async Task CommitTerminalWriteAsync(IStore store)
    {
        await using var session = store.CreateSession();

        var interaction = await session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.InteractionId == "interaction-1")
            .FirstOrDefaultAsync();

        interaction.Outcome = CallOutcome.Completed;
        interaction.EndedUtc = _startedUtc.AddMinutes(1);
        await session.SaveAsync(interaction, checkConcurrency: true);
        await session.SaveChangesAsync();
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"crestapps-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new TelephonyInteractionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        return store;
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new TelephonyInteractionMigrations
        {
            SchemaBuilder = schemaBuilder,
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var interactionStore = new DefaultTelephonyInteractionStore(session, store);
        var interaction = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "ProviderA",
            UserId = "user-1",
            UserName = "agent",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _startedUtc,
        };
        await interactionStore.CreateAsync(interaction, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<TelephonyInteraction> ReadAsync(IStore store, string interactionId)
    {
        await using var session = store.CreateSession();

        return await session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.InteractionId == interactionId)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }
}
