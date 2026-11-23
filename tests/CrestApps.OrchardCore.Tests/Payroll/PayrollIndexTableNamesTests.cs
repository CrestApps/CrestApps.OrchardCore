using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.AspNetCore.DataProtection;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Payroll's index tables have module-specific names, so an application module that keeps an index of the same generic
/// name (such as PayRunIndex) in a collection named Payroll never shares a table with them; a site made before the rename
/// keeps its index rows, on SQLite with a table prefix as a tenant has.
/// </summary>
public sealed class PayrollIndexTableNamesTests
{
    private static readonly (string Legacy, Type Index)[] _moved =
    [
        ("Payroll_PayScheduleIndex", typeof(PayrollPayScheduleIndex)),
        ("Payroll_PayCodeIndex", typeof(PayrollPayCodeIndex)),
        ("Payroll_EmployerIndex", typeof(PayrollEmployerIndex)),
        ("Payroll_PayProfileIndex", typeof(PayrollPayProfileIndex)),
        ("Payroll_PayItemIndex", typeof(PayrollPayItemIndex)),
        ("Payroll_PayStatementIndex", typeof(PayrollPayStatementIndex)),
        ("Payroll_PayStatementLineIndex", typeof(PayrollPayStatementLineIndex)),
        ("Payroll_PayRunIndex", typeof(PayrollPayRunIndex)),
        ("Payroll_PayRunInputIndex", typeof(PayrollPayRunInputIndex)),
        ("Payroll_TaxDepositIndex", typeof(PayrollTaxDepositIndex)),
        ("Payroll_GarnishmentOrderIndex", typeof(PayrollGarnishmentOrderIndex)),
    ];

    [Fact]
    public async Task NewSite_MakesOnlyModuleSpecificTables_SoAnAppIndexOfTheSameGenericNameFits()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            // An application module's own pay run index, in its own Payroll collection.
            await schemaBuilder.CreateTableAsync("Payroll_PayRunIndex", table => table
                .Column<long>("Id", column => column.PrimaryKey().Identity())
                .Column<long>("DocumentId")
                .Column<int>("Status"));

            await PaySetupIndexSchema.CreateAsync(schemaBuilder);
            await PayrollIndexSchema.CreateAsync(schemaBuilder);
            await PayrollFilingIndexSchema.CreateAsync(schemaBuilder);
            await GarnishmentOrderIndexSchema.CreateAsync(schemaBuilder);

            foreach (var (legacy, index) in _moved)
            {
                Assert.True(await TableExistence.ExistsAsync(schemaBuilder, NewTable(schemaBuilder, index)), index.Name);
                Assert.StartsWith("Payroll", index.Name, StringComparison.Ordinal);
            }

            Assert.True(await TableExistence.ExistsAsync(schemaBuilder, "Payroll_PayRunIndex"));
            Assert.False(await TableExistence.ExistsAsync(schemaBuilder, "Payroll_EmployerIndex"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task SiteMadeBeforeTheRename_KeepsItsRows_InTheNewTables_AndTheOldTablesGo()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            // The tables of a site made before the rename, each with one row.
            foreach (var (legacy, index) in _moved)
            {
                await CreateLegacyTableAsync(schemaBuilder, legacy, index);
                await InsertRowAsync(schemaBuilder, store, legacy, index);
            }

            var hasher = new PayrollTaxIdHasher(session, new EphemeralDataProtectionProvider(), new KeyedDistributedLock());

            var paySetup = await new PaySetupMigrations(null, store) { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
            var payroll = await new PayrollMigrations(null, null, hasher, store) { SchemaBuilder = schemaBuilder }.UpdateFrom10Async();
            var garnishments = await new GarnishmentsMigrations(null, store) { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

            Assert.Equal((2, 11, 2), (paySetup, payroll, garnishments));

            foreach (var (legacy, index) in _moved)
            {
                Assert.False(await TableExistence.ExistsAsync(schemaBuilder, legacy), legacy);
                Assert.True(await IndexMigrationSql.ExistsAsync(schemaBuilder, $"SELECT 1 FROM {IndexMigrationSql.GetQuotedTableName(schemaBuilder, store, index, PayrollConstants.CollectionName)} WHERE \"DocumentId\" = 42"), index.Name);
            }

            // The key tax ids are hashed with was made in the migration's own unit of work.
            Assert.NotNull(await session.Query<PayrollKeys>(collection: PayrollConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task SiteWhoseOlderStepMadeANewTable_IsMovedWithoutMakingItAgain()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            // A site made with only the employers table, whose later steps made the other tables under their new names.
            await CreateLegacyTableAsync(schemaBuilder, "Payroll_EmployerIndex", typeof(PayrollEmployerIndex));
            await PayrollIndexSchema.CreateProfileTablesAsync(schemaBuilder);
            await PayStatementIndexSchema.CreateAsync(schemaBuilder);
            await PayStatementIndexSchema.AddFlatWagesAsync(schemaBuilder);
            await PayRunIndexSchema.CreateAsync(schemaBuilder);
            await PayrollFilingIndexSchema.CreateAsync(schemaBuilder);

            var hasher = new PayrollTaxIdHasher(session, new EphemeralDataProtectionProvider(), new KeyedDistributedLock());
            var version = await new PayrollMigrations(null, null, hasher, store) { SchemaBuilder = schemaBuilder }.UpdateFrom10Async();

            Assert.Equal(11, version);
            Assert.False(await TableExistence.ExistsAsync(schemaBuilder, "Payroll_EmployerIndex"));
            Assert.True(await TableExistence.ExistsAsync(schemaBuilder, NewTable(schemaBuilder, typeof(PayrollEmployerIndex))));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Task CreateLegacyTableAsync(SchemaBuilder schemaBuilder, string name, Type indexType)
        => schemaBuilder.CreateTableAsync(name, table =>
        {
            table.Column<long>("Id", column => column.PrimaryKey().Identity());

            foreach (var column in PayrollIndexTableMove.ColumnsOf(indexType))
            {
                var type = column == "DocumentId" ? typeof(long) : indexType.GetProperty(column).PropertyType;

                table.Column(column, type, options => options.Nullable());
            }
        });

    // One row of the old table, with the document id 42 and a value in every column.
    private static Task InsertRowAsync(SchemaBuilder schemaBuilder, IStore store, string table, Type indexType)
    {
        var columns = PayrollIndexTableMove.ColumnsOf(indexType);
        var values = columns.Select(column => column == "DocumentId"
            ? "42"
            : indexType.GetProperty(column).PropertyType == typeof(string) ? "'x'" : "1");

        return IndexMigrationSql.ExecuteAsync(
            schemaBuilder,
            $"INSERT INTO {Quoted(schemaBuilder, store, table)} ({string.Join(", ", columns.Select(schemaBuilder.Dialect.QuoteForColumnName))}) VALUES ({string.Join(", ", values)})");
    }

    private static string NewTable(SchemaBuilder schemaBuilder, Type indexType)
        => schemaBuilder.TableNameConvention.GetIndexTable(indexType, PayrollConstants.CollectionName);

    private static string Quoted(SchemaBuilder schemaBuilder, IStore store, string table)
        => schemaBuilder.Dialect.QuoteForTableName(schemaBuilder.TablePrefix + table, store.Configuration.Schema);

    private static string DatabasePath()
        => Path.Combine(Path.GetTempPath(), $"payroll-index-names-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix("tenant_"));

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(PayrollConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }
}
