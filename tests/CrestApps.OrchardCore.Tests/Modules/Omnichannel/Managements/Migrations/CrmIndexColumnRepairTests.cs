using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

/// <summary>
/// A tenant can hold the lead and opportunity index tables from an earlier install. The create step kept such a table
/// and only added <c>SourceId</c> to it, so a table without a later column stayed that way, and live the Opportunities
/// list failed with "Invalid column name 'IsClosed'".
/// </summary>
public sealed class CrmIndexColumnRepairTests
{
    [Fact]
    public async Task UpdateFrom6_WhenTheIndexTablesLackColumns_AddsThemAndLeavesTheTablesAloneTheNextTime()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"crm-index-repair-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        try
        {
            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
                await CreateEarlierLeadIndexAsync(schemaBuilder);
                await CreateEarlierOpportunityIndexAsync(schemaBuilder);
                var migration = CreateMigration(store, databasePath, schemaBuilder);

                Assert.Equal(7, await migration.UpdateFrom6Async());

                var leadSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName<LeadIndex>(store));
                var opportunitySchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName<OpportunityIndex>(store));

                Assert.Contains("IsClosed type=", leadSchema);
                Assert.Contains("ConvertedUtc type=", leadSchema);
                Assert.Contains("IsClosed type=", opportunitySchema);
                Assert.Contains("AccountContentItemId type=", opportunitySchema);

                // A table that already has every column, on a fresh tenant or after this step ran, is left as it is.
                Assert.Equal(7, await migration.UpdateFrom6Async());
                Assert.Equal(leadSchema, await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName<LeadIndex>(store)));
                Assert.Equal(opportunitySchema, await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName<OpportunityIndex>(store)));

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using var committed = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await committed.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Contains("IsClosed type=", await SqliteSchemaSnapshot.CaptureAsync(committed, null, TableName<OpportunityIndex>(store)));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateFrom6_WhenTheIndexTablesAreMissing_ChangesNothing()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"crm-index-repair-missing-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            var migration = CreateMigration(store, databasePath, schemaBuilder);

            Assert.Equal(7, await migration.UpdateFrom6Async());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static CrmMigrations CreateMigration(IStore store, string databasePath, SchemaBuilder schemaBuilder)
    {
        return new CrmMigrations(
            Mock.Of<IContentDefinitionManager>(),
            store,
            new SqliteConnectionAccessor(databasePath),
            NullLogger<CrmMigrations>.Instance)
        {
            SchemaBuilder = schemaBuilder,
        };
    }

    // The lead index as an earlier install left it: no closed, conversion or creation columns.
    private static async Task CreateEarlierLeadIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<LeadIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("StatusId", column => column.WithLength(50))
            .Column<string>("OwnerId", column => column.WithLength(50)));
    }

    // The opportunity index as an earlier install left it: no closed, won or account columns.
    private static async Task CreateEarlierOpportunityIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OpportunityIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("StageId", column => column.WithLength(50))
            .Column<decimal>("Amount", column => column.Nullable()));
    }

    private static string TableName<TIndex>(IStore store)
        => store.Configuration.TablePrefix + store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), null);

    private sealed class SqliteConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;

        public SqliteConnectionAccessor(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Pooling=False";
        }

        public DbConnection CreateConnection()
            => new SqliteConnection(_connectionString);
    }
}
