using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the indexes the call path and provider webhooks seek through, on a fresh tenant and on a tenant that runs the
/// step as an upgrade, and requires the step to be safe to run again.
/// </summary>
/// <remarks>
/// Each lookup filters on a column no other index leads with, so without these indexes every call and every webhook
/// scans the table. Nothing functional fails when one is missing; the read only gets slower as the table grows, so the
/// shape itself is the evidence.
/// </remarks>
public sealed class ContactCenterCallPathLookupIndexMigrationTests
{
    [Fact]
    public Task QueueItemIndex_HasTheActivityLookupIndex()
        => AssertLookupIndexesAsync<QueueItemIndex, QueueItemIndexMigrations>(
            store => new QueueItemIndexMigrations(store, new StubClock()),
            m => m.CreateAsync(),
            m => m.UpdateFrom8Async(),
            expectedVersion: 9,
            ("IDX_QueueItemIndex_ActivityItemId", "ActivityItemId, EnqueuedUtc, DocumentId"));

    [Fact]
    public Task InteractionIndex_HasTheProviderAndCorrelationLookupIndexes()
        => AssertLookupIndexesAsync<InteractionIndex, InteractionIndexMigrations>(
            store => new InteractionIndexMigrations(store),
            m => m.CreateAsync(),
            m => m.UpdateFrom9Async(),
            expectedVersion: 10,
            expectedFreshVersion: 11,
            ("IDX_InteractionIndex_ProviderId", "ProviderInteractionId, ProviderName, CreatedUtc"),
            ("IDX_InteractionIndex_CorrelationId", "CorrelationId, CreatedUtc"));

    [Fact]
    public Task InteractionIndex_HasTheRecentByAgentIndex()
        => AssertLookupIndexesAsync<InteractionIndex, InteractionIndexMigrations>(
            store => new InteractionIndexMigrations(store),
            m => m.CreateAsync(),
            m => m.UpdateFrom10Async(),
            expectedVersion: 11,
            ("IDX_InteractionIndex_RecentByAgent", "AgentId, CreatedUtc"));

    [Fact]
    public Task CallSessionIndex_HasTheProviderCallAndInteractionLookupIndexes()
        => AssertLookupIndexesAsync<CallSessionIndex, CallSessionIndexMigrations>(
            store => new CallSessionIndexMigrations(store, new ProviderIdentityResolver([])),
            m => m.CreateAsync(),
            m => m.UpdateFrom7Async(),
            expectedVersion: 8,
            ("IDX_CallSessionIndex_ProviderCallId", "ProviderCallId, ProviderName, CreatedUtc"),
            ("IDX_CallSessionIndex_InteractionId", "InteractionId, CreatedUtc"));

    [Fact]
    public Task ContactCenterOutboxMessageIndex_HasTheEventLookupIndex()
        => AssertLookupIndexesAsync<ContactCenterOutboxMessageIndex, ContactCenterOutboxMessageIndexMigrations>(
            store => new ContactCenterOutboxMessageIndexMigrations(store, new StubClock()),
            m => m.CreateAsync(),
            m => m.UpdateFrom3Async(),
            expectedVersion: 4,
            ("IDX_ContactCenterOutboxMessageIndex_EventId", "EventId, DocumentId"));

    /// <summary>
    /// Builds a fresh tenant through its creation and upgrade chain, then a tenant that reaches the new step from the
    /// version before it, and requires both to carry the lookup indexes, non-unique, with the same table shape, and
    /// the step to change nothing when it runs again.
    /// </summary>
    private static Task AssertLookupIndexesAsync<TIndex, TMigration>(
        Func<IStore, TMigration> createMigration,
        Func<TMigration, Task<int>> create,
        Func<TMigration, Task<int>> lookupStep,
        int expectedVersion,
        params (string Name, string Columns)[] expectedIndexes)
        where TMigration : DataMigration
        => AssertLookupIndexesAsync<TIndex, TMigration>(createMigration, create, lookupStep, expectedVersion, expectedVersion, expectedIndexes);

    /// <summary>
    /// As above, for a step that is not the last one of the chain: the step returns <paramref name="expectedVersion"/>,
    /// and a fresh tenant ends the chain at <paramref name="expectedFreshVersion"/>.
    /// </summary>
    private static async Task AssertLookupIndexesAsync<TIndex, TMigration>(
        Func<IStore, TMigration> createMigration,
        Func<TMigration, Task<int>> create,
        Func<TMigration, Task<int>> lookupStep,
        int expectedVersion,
        int expectedFreshVersion,
        params (string Name, string Columns)[] expectedIndexes)
        where TMigration : DataMigration
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-lookup-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            var migration = createMigration(store);
            migration.SchemaBuilder = schemaBuilder;
            var tableName = store.Configuration.TablePrefix +
                store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), ContactCenterStorage.CollectionName);

            // A fresh tenant runs its creation step and then every upgrade step reachable from the version it returns.
            var freshVersion = await MigrationChainRunner.RunUpgradeChainAsync(migration, await create(migration));
            var freshSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, tableName);

            Assert.Equal(expectedFreshVersion, freshVersion);

            foreach (var (name, columns) in expectedIndexes)
            {
                Assert.Contains($"{name} unique=False columns=({columns})", freshSchema, StringComparison.Ordinal);
            }

            // A tenant already at the previous version has every other index but these, so dropping them reproduces
            // its shape, and running the step on it is the upgrade a deployed tenant takes.
            foreach (var (name, _) in expectedIndexes)
            {
                await using var command = schemaBuilder.Connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"DROP INDEX \"{name}\"";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(expectedVersion, await lookupStep(migration));
            Assert.Equal(freshSchema, await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, tableName));

            // Two instances activating the same tenant can both run the step, so the second run must find the
            // indexes and change nothing rather than fail the activation.
            Assert.Equal(expectedVersion, await lookupStep(migration));
            Assert.Equal(freshSchema, await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, tableName));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }
}
