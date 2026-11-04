using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Migrations;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Migrations;

/// <summary>
/// Pins the indexes the omnichannel message reads and the telephony call webhooks seek, on a fresh tenant and on a
/// tenant that takes the step as an upgrade, and requires the step to be safe to run again.
/// </summary>
/// <remarks>
/// Every earlier index on the message table led with <c>DocumentId</c>, and the call id was carried only by an index
/// led by the user, so each of these reads scanned its table. Nothing functional fails without them; the reads only get
/// slower as the tables grow, so the shape itself is the evidence.
/// </remarks>
public sealed class MessageAndCallLookupIndexMigrationTests
{
    [Fact]
    public Task OmnichannelMessageIndex_HasTheChannelProviderAndConversationIndexes()
        => AssertLookupIndexesAsync<OmnichannelMessageIndex, OmnichannelMessageIndexMigrations>(
            new OmnichannelMessageIndexMigrations(),
            OmnichannelConstants.CollectionName,
            migration => migration.CreateAsync(),
            migration => migration.UpdateFrom4Async(),
            expectedVersion: 5,
            ("IDX_OmnichannelMessageIndex_ChannelDirection", ["Channel", "IsInbound", "CreatedUtc", "DocumentId"]),
            ("IDX_OmnichannelMessageIndex_ProviderMessage", ["Channel", "ProviderMessageId", "DocumentId"]),
            ("IDX_OmnichannelMessageIndex_Conversation", ["ConversationId", "CreatedUtc", "DocumentId"]));

    [Fact]
    public Task TelephonyInteractionIndex_HasTheProviderCallIndex()
        => AssertLookupIndexesAsync<TelephonyInteractionIndex, TelephonyInteractionMigrations>(
            new TelephonyInteractionMigrations(),
            collection: null,
            migration => migration.CreateAsync(),
            migration => migration.UpdateFrom3Async(),
            expectedVersion: 4,
            ("IDX_TelephonyInteractionIndex_ProviderCall", ["CallId", "ProviderName", "DocumentId"]));

    private static async Task AssertLookupIndexesAsync<TIndex, TMigration>(
        TMigration migration,
        string collection,
        Func<TMigration, Task<int>> create,
        Func<TMigration, Task<int>> lookupStep,
        int expectedVersion,
        params (string Name, string[] Columns)[] expectedIndexes)
        where TMigration : DataMigration
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = Path.Combine(Path.GetTempPath(), $"lookup-index-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        try
        {
            await store.InitializeAsync(cancellationToken);

            if (collection is not null)
            {
                await store.InitializeCollectionAsync(collection, cancellationToken);
            }

            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(cancellationToken));
            migration.SchemaBuilder = schemaBuilder;

            // A fresh tenant gets the indexes from its creation step and records the final version.
            Assert.Equal(expectedVersion, await create(migration));

            foreach (var (name, columns) in expectedIndexes)
            {
                Assert.Equal(columns, await IndexColumnsAsync<TIndex>(schemaBuilder, name, collection, cancellationToken));
            }

            // A tenant at the previous version has every other index but these.
            foreach (var (name, _) in expectedIndexes)
            {
                await schemaBuilder.AlterIndexTableAsync<TIndex>(table => table.DropIndex(name), collection);
            }

            Assert.Equal(expectedVersion, await lookupStep(migration));

            foreach (var (name, columns) in expectedIndexes)
            {
                Assert.Equal(columns, await IndexColumnsAsync<TIndex>(schemaBuilder, name, collection, cancellationToken));
            }

            // Two instances activating the same tenant can both run the step, so the second finds the indexes.
            Assert.Equal(expectedVersion, await lookupStep(migration));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The indexed columns in order, or none when the index is missing.
    private static async Task<string[]> IndexColumnsAsync<TIndex>(
        SchemaBuilder schemaBuilder,
        string indexName,
        string collection,
        CancellationToken cancellationToken)
    {
        if (!await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(TIndex), indexName, collection))
        {
            return [];
        }

        var effectiveName = schemaBuilder.Dialect.FormatIndexName(
            schemaBuilder.Dialect.PrefixIndex ? schemaBuilder.TablePrefix + indexName : indexName);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT name FROM pragma_index_info('{effectiveName}') ORDER BY seqno";

        var columns = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return [.. columns];
    }
}
