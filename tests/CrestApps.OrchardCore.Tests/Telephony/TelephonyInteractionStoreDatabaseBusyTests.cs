using System.Data.Common;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards the 'database is locked' failure that dropped a caller's hangup. The provider webhook's own scope had
/// already written (the hangup's call-quality record) when the telephony call-history projection updated the call
/// through its isolated session. SQLite has a single writer, and that writer was the webhook's own open transaction,
/// so the isolated write waited out the whole busy timeout (about 30 seconds live) and failed with SQLite error 5.
/// The Contact Center projection never ran, the call stayed up in the Contact Center, and a reconciliation sweep
/// ended it half a minute late.
/// </summary>
public sealed class TelephonyInteractionStoreDatabaseBusyTests
{
    private static readonly DateTime _startedUtc = new(2026, 9, 24, 14, 4, 11, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateByProviderCallIdAsync_WhenTheCallersOwnSessionHoldsTheWriteLock_DoesNotDeadlockOnIt()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-ambient-writer");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, "interaction-1", "call-1");

            await using var ambient = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(ambient, store, new ProviderIdentityResolver([]), []);

            // What the webhook scope did before the projection ran: it wrote something of its own, and a query
            // flushed it, so its transaction now holds the database's only write lock.
            await ambient.SaveAsync(NewInteraction("interaction-2", "call-2"), cancellationToken: TestContext.Current.CancellationToken);
            await ambient.FlushAsync(TestContext.Current.CancellationToken);

            // Act
            var updated = await interactionStore.UpdateByProviderCallIdAsync(
                "ProviderA",
                "call-1",
                candidate =>
                {
                    candidate.Outcome = CallOutcome.Completed;
                    candidate.EndedUtc = _startedUtc.AddMinutes(2);

                    return true;
                },
                TestContext.Current.CancellationToken);

            await ambient.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(CallOutcome.Completed, (await ReadAsync(store, "interaction-1")).Outcome);

            // The ambient work is kept, not lost to the projection's write.
            Assert.NotNull(await ReadAsync(store, "interaction-2"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateByProviderCallIdAsync_WhenAnotherWriterBrieflyHoldsTheDatabase_RetriesInsteadOfFailing()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-busy-retry");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, "interaction-1", "call-1");

            // A different writer (a background sweep, another webhook) holds the write lock for longer than one
            // attempt's busy timeout, then commits.
            var competing = store.CreateSession();
            await competing.SaveAsync(NewInteraction("interaction-2", "call-2"), cancellationToken: TestContext.Current.CancellationToken);
            await competing.FlushAsync(TestContext.Current.CancellationToken);

            var release = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1600));
                await competing.SaveChangesAsync();
                await competing.DisposeAsync();
            }, TestContext.Current.CancellationToken);

            await using var ambient = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(ambient, store, new ProviderIdentityResolver([]), []);

            // Act
            var updated = await interactionStore.UpdateByProviderCallIdAsync(
                "ProviderA",
                "call-1",
                candidate =>
                {
                    candidate.Outcome = CallOutcome.Completed;

                    return true;
                },
                TestContext.Current.CancellationToken);

            await release;

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(CallOutcome.Completed, (await ReadAsync(store, "interaction-1")).Outcome);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateByProviderCallIdAsync_WhenTheDatabaseStaysLocked_GivesUpAndPropagatesTheError()
    {
        // Arrange
        var databasePath = DatabasePath("telephony-busy-bounded");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, "interaction-1", "call-1");

            // A writer that never lets go. The retry is bounded, and the error reaches the caller so the durable
            // webhook inbox schedules the delivery again rather than it being swallowed.
            await using var competing = store.CreateSession();
            await competing.SaveAsync(NewInteraction("interaction-2", "call-2"), cancellationToken: TestContext.Current.CancellationToken);
            await competing.FlushAsync(TestContext.Current.CancellationToken);

            await using var ambient = store.CreateSession();
            var interactionStore = new DefaultTelephonyInteractionStore(ambient, store, new ProviderIdentityResolver([]), []);
            var attempts = 0;

            // Act
            var exception = await Record.ExceptionAsync(() => interactionStore.UpdateByProviderCallIdAsync(
                "ProviderA",
                "call-1",
                candidate =>
                {
                    attempts++;
                    candidate.Outcome = CallOutcome.Completed;

                    return true;
                },
                TestContext.Current.CancellationToken));

            await competing.CancelAsync();

            // Assert
            var databaseError = Assert.IsAssignableFrom<DbException>(exception);
            Assert.Contains("locked", databaseError.Message, StringComparison.OrdinalIgnoreCase);
            Assert.InRange(attempts, 2, 5);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static TelephonyInteraction NewInteraction(string interactionId, string callId)
        => new()
        {
            InteractionId = interactionId,
            CallId = callId,
            ProviderName = "ProviderA",
            UserId = "user-1",
            UserName = "agent",
            Direction = CallDirection.Outbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _startedUtc,
        };

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"crestapps-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        // A one-second busy timeout stands in for the live thirty seconds, so a write that cannot get the lock fails
        // quickly instead of stalling the test.
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False;Default Timeout=1"));
        store.RegisterIndexes([new TelephonyInteractionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new TelephonyInteractionMigrations
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SeedAsync(IStore store, string interactionId, string callId)
    {
        await using var session = store.CreateSession();
        await session.SaveAsync(NewInteraction(interactionId, callId), cancellationToken: TestContext.Current.CancellationToken);
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
