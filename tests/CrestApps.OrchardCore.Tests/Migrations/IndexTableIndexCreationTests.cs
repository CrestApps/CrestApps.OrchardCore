using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Migrations;

/// <summary>
/// Covers the upgrade steps that add an index when a second app instance has already added it.
/// </summary>
/// <remarks>
/// Two instances activating the same tenant at once both read the old version: the second waits on the first's
/// schema lock and then finds the index the first created. Creating it again failed with "an index with that name
/// already exists", which cancelled every migration still pending in that activation.
/// </remarks>
public sealed class IndexTableIndexCreationTests
{
    private const string TablePrefix = "tenant_";
    private const string ItemIdIndexName = "IDX_AgentProfileIndex_ItemId";

    [Fact]
    public async Task CreateIfMissingAsync_WhenTheIndexIsMissing_CreatesIt()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));
            await CreateVersion1TableAsync(schemaBuilder);

            var created = await IndexTableIndexCreation.CreateIfMissingAsync<AgentProfileIndex>(
                schemaBuilder,
                ItemIdIndexName,
                ContactCenterStorage.CollectionName,
                "ItemId", "DocumentId");

            Assert.True(created);
            Assert.True(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(AgentProfileIndex), ItemIdIndexName, ContactCenterStorage.CollectionName));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeStep_WhenAnotherInstanceAlreadyAddedTheIndex_RecordsTheVersionInsteadOfFailing()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));
            await CreateVersion1TableAsync(schemaBuilder);

            // What the other instance committed while this one still read version 1.
            await schemaBuilder.AlterIndexTableAsync<AgentProfileIndex>(table => table
                .CreateIndex(ItemIdIndexName, "ItemId", "DocumentId"),
                collection: ContactCenterStorage.CollectionName);

            var migration = new AgentProfileIndexMigrations
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ExistsAsync_DoesNotMatchAnIndexOfTheSameNameOnAnotherTable()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));
            await CreateVersion1TableAsync(schemaBuilder);

            Assert.False(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(AgentProfileIndex), ItemIdIndexName, ContactCenterStorage.CollectionName));
            Assert.False(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(AgentProfileIndex), "IDX_AgentProfileIndex_DocumentId", collection: null));
            Assert.True(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(AgentProfileIndex), "IDX_AgentProfileIndex_DocumentId", ContactCenterStorage.CollectionName));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The shape version 1 released: no index leading with ItemId.
    private static async Task CreateVersion1TableAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<AgentPresenceStatus>("PresenceStatus"),
            collection: ContactCenterStorage.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<AgentProfileIndex>(table => table
            .CreateIndex("IDX_AgentProfileIndex_DocumentId", "DocumentId", "ItemId", "UserId", "PresenceStatus"),
            collection: ContactCenterStorage.CollectionName);
    }

    private static string DatabasePath()
        => Path.Combine(Path.GetTempPath(), $"index-creation-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix(TablePrefix));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
