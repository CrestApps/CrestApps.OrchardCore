using System.Data.Common;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Dapper;
using Microsoft.Data.SqlClient;
using Moq;
using OrchardCore;
using OrchardCore.Documents;
using OrchardCore.Recipes.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The upkeep columns of the response index read 0 on rows that were in the table before the upgrade, on SQL Server
/// too, where a nullable column added with a default leaves the existing rows empty and upkeep's filters skip them.
/// The SQL Server tests run against LocalDB and are skipped where it isn't available (Linux CI). The update builds its
/// SQL by hand, so it also runs with a table prefix, and on SQL Server with a database schema, as a tenant's tables are.
/// </summary>
public sealed class SurveysMigrationsSqlTests
{
    private const string LocalDbServer = @"(localdb)\MSSQLLocalDB";
    private const string Responses = SurveysConstants.ResponsesCollectionName;
    private const string LocalDbMissing = "SQL Server LocalDB is not available (it runs on Windows only).";
    private const string TablePrefix = "tenant1_";
    private const string Schema = "tenant1";

    private static readonly Lazy<bool> _localDbAvailable = new(ProbeLocalDb);

    /// <summary>
    /// Gets whether SQL Server LocalDB answers. The SQL Server tests skip through their attribute when it doesn't: a skip
    /// thrown from inside a test is reported as a failure on CI.
    /// </summary>
    public static bool IsLocalDbAvailable => _localDbAvailable.Value;

    [Fact]
    public void AddingTheUpkeepColumns_OnSqlServer_DeclaresThemNotNullWithADefault()
    {
        // Arrange
        var configuration = new global::YesSql.Configuration().UseSqlServer("Server=unused;Database=unused");
        var command = new AlterTableCommand("SurveyResponses_SurveyResponseIndex", configuration.SqlDialect, string.Empty);

        // Act
        SurveysIndexSchema.AddResponseUpkeepColumns(command);
        var statements = configuration.CommandInterpreter.CreateSql([command]).ToList();

        // Assert
        Assert.Contains(statements, statement => statement.Contains("[Anonymized]", StringComparison.Ordinal) && statement.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase) && statement.Contains("default 0", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(statements, statement => statement.Contains("[UpkeepFailures]", StringComparison.Ordinal) && statement.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase) && statement.Contains("default 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateFrom2_OnSqlite_ExistingRowsReadZero()
    {
        await using var database = SqliteDatabase.Create();

        var rows = await UpgradeAsync(database.Store, beforeUpdate: null, migration => migration.UpdateFrom2Async(), expectedVersion: 3);

        AssertAllZero(rows);
    }

    [Fact]
    public async Task UpdateFrom3_OnSqlite_FillsEmptyUpkeepValues()
    {
        await using var database = SqliteDatabase.Create();

        var rows = await UpgradeAsync(database.Store, AddNullableUpkeepColumnsAsync(clearValues: true), migration => migration.UpdateFrom3Async(), expectedVersion: 4);

        AssertAllZero(rows);
    }

    [Fact]
    public async Task UpdateFrom3_OnSqliteWithATablePrefix_FillsEmptyUpkeepValues()
    {
        await using var database = SqliteDatabase.Create(TablePrefix);

        var rows = await UpgradeAsync(database.Store, AddNullableUpkeepColumnsAsync(clearValues: true), migration => migration.UpdateFrom3Async(), expectedVersion: 4);

        AssertAllZero(rows);
        await AssertTheTablesArePrefixedAsync(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom2_OnSqlServer_ExistingRowsReadZero()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        var rows = await UpgradeAsync(database.Store, beforeUpdate: null, migration => migration.UpdateFrom2Async(), expectedVersion: 3);

        AssertAllZero(rows);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom3_OnSqlServer_FillsTheRowsTheNullableColumnsLeftEmpty()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        // The earlier, nullable columns: SQL Server leaves the rows already in the table empty.
        var rows = await UpgradeAsync(database.Store, AddNullableUpkeepColumnsAsync(clearValues: false), migration => migration.UpdateFrom3Async(), expectedVersion: 4);

        AssertAllZero(rows);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom2_OnSqlServerWithATablePrefixAndASchema_ExistingRowsReadZero()
    {
        await using var database = await LocalDbDatabase.CreateAsync(TablePrefix, Schema);

        var rows = await UpgradeAsync(database.Store, beforeUpdate: null, migration => migration.UpdateFrom2Async(), expectedVersion: 3);

        AssertAllZero(rows);
        await AssertTheTablesArePrefixedAsync(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom3_OnSqlServerWithATablePrefixAndASchema_FillsTheRowsTheNullableColumnsLeftEmpty()
    {
        await using var database = await LocalDbDatabase.CreateAsync(TablePrefix, Schema);

        // The raw UPDATE must name the table with the tenant's prefix and schema: unqualified, it would miss it.
        var rows = await UpgradeAsync(database.Store, AddNullableUpkeepColumnsAsync(clearValues: false), migration => migration.UpdateFrom3Async(), expectedVersion: 4);

        AssertAllZero(rows);
        await AssertTheTablesArePrefixedAsync(database.Store);
    }

    // The index table exists only under the tenant's prefix (and schema), so the update can't have found another one.
    private static async Task AssertTheTablesArePrefixedAsync(IStore store)
    {
        var plainName = store.Configuration.TableNameConvention.GetIndexTable(typeof(SurveyResponseIndex), Responses);
        var schema = store.Configuration.Schema;
        var tables = new List<(string Schema, string Name)>();

        await InTransactionAsync(store, async builder =>
        {
            var sql = store.Configuration.SqlDialect is SqlServerDialect
                ? "SELECT TABLE_SCHEMA AS SchemaName, TABLE_NAME AS Name FROM INFORMATION_SCHEMA.TABLES"
                : "SELECT '' AS SchemaName, name AS Name FROM sqlite_master WHERE type = 'table'";

            foreach (var row in await builder.Connection.QueryAsync(sql, transaction: builder.Transaction))
            {
                var values = (IDictionary<string, object>)row;

                tables.Add((values["SchemaName"] as string, values["Name"] as string));
            }
        });

        Assert.Contains(tables, table => table.Name == TablePrefix + plainName && (schema is null || table.Schema == schema));
        Assert.DoesNotContain(tables, table => table.Name == plainName);
    }

    private static void AssertAllZero(IReadOnlyList<(object Anonymized, object Failures, bool Matched)> rows)
    {
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.Anonymized);
            Assert.NotNull(row.Failures);
            Assert.False(Convert.ToBoolean(row.Anonymized, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0, Convert.ToInt32(row.Failures, System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(row.Matched, "Upkeep's filters (not anonymized, fewer than the most failures) skip the row.");
        });
    }

    private static Func<ISchemaBuilder, string, Task> AddNullableUpkeepColumnsAsync(bool clearValues)
        => async (builder, schema) =>
        {
            await builder.AlterIndexTableAsync<SurveyResponseIndex>(table =>
            {
                table.AddColumn<bool>("Anonymized", column => column.WithDefault(false));
                table.AddColumn<int>("UpkeepFailures", column => column.WithDefault(0));
            }, collection: Responses);

            var dialect = builder.Dialect;

            if (clearValues)
            {
                // SQLite fills a nullable column's default, so the empty values SQL Server leaves are set by hand.
                await builder.Connection.ExecuteAsync(
                    $"UPDATE {IndexTable(builder, schema)} SET {dialect.QuoteForColumnName("Anonymized")} = NULL, {dialect.QuoteForColumnName("UpkeepFailures")} = NULL",
                    transaction: builder.Transaction);
            }
            else
            {
                // The bug the update repairs: SQL Server left the rows that were there empty.
                Assert.Equal(2, await builder.Connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {IndexTable(builder, schema)} WHERE {dialect.QuoteForColumnName("Anonymized")} IS NULL AND {dialect.QuoteForColumnName("UpkeepFailures")} IS NULL",
                    transaction: builder.Transaction));
            }
        };

    // Makes the response index as an earlier version had it (no upkeep columns) with two rows, runs what came before
    // the update, then the update, and reads the upkeep columns back with upkeep's own filters.
    private static async Task<IReadOnlyList<(object Anonymized, object Failures, bool Matched)>> UpgradeAsync(
        IStore store,
        Func<ISchemaBuilder, string, Task> beforeUpdate,
        Func<SurveysMigrations, Task<int>> update,
        int expectedVersion)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var schema = store.Configuration.Schema;

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(Responses, cancellationToken);

        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyResponseIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20)),
            collection: Responses));

        // The documents are saved with no index provider registered, so the rows are written by hand below, as the
        // earlier version's provider would have.
        await using (var session = store.CreateSession())
        {
            await session.SaveAsync(new SurveyResponse { ItemId = IdGenerator.GenerateId() }, collection: Responses, cancellationToken: cancellationToken);
            await session.SaveAsync(new SurveyResponse { ItemId = IdGenerator.GenerateId() }, collection: Responses, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await InTransactionAsync(store, async builder =>
        {
            var dialect = builder.Dialect;
            var documents = dialect.QuoteForTableName(builder.TablePrefix + builder.TableNameConvention.GetDocumentTable(Responses), store.Configuration.Schema);

            await builder.Connection.ExecuteAsync(
                $"INSERT INTO {IndexTable(builder, schema)} ({dialect.QuoteForColumnName("DocumentId")}, {dialect.QuoteForColumnName("ItemId")}, {dialect.QuoteForColumnName("Status")}) " +
                $"SELECT {dialect.QuoteForColumnName("Id")}, 'r', 'Completed' FROM {documents}",
                transaction: builder.Transaction);
        });

        if (beforeUpdate is not null)
        {
            await InTransactionAsync(store, builder => beforeUpdate(builder, schema));
        }

        var version = 0;

        await InTransactionAsync(store, async builder =>
        {
            var migration = new SurveysMigrations(Mock.Of<IRecipeMigrator>(), new ResumableSteps(Mock.Of<IDocumentManager<ResumableStepsDocument>>()), store)
            {
                SchemaBuilder = builder,
            };

            version = await update(migration);
        });

        Assert.Equal(expectedVersion, version);

        IReadOnlyList<(object, object, bool)> rows = null;

        await InTransactionAsync(store, async builder =>
        {
            var dialect = builder.Dialect;
            var anonymized = dialect.QuoteForColumnName("Anonymized");
            var failures = dialect.QuoteForColumnName("UpkeepFailures");
            var read = await builder.Connection.QueryAsync(
                $"SELECT {anonymized} AS Anonymized, {failures} AS Failures, " +
                $"CASE WHEN {anonymized} = @NotAnonymized AND {failures} < 5 THEN 1 ELSE 0 END AS Matched FROM {IndexTable(builder, schema)}",
                new { NotAnonymized = false },
                builder.Transaction);

            rows = read.Select(row =>
            {
                var values = (IDictionary<string, object>)row;

                return (values["Anonymized"], values["Failures"], Convert.ToInt32(values["Matched"], System.Globalization.CultureInfo.InvariantCulture) == 1);
            }).ToList();
        });

        return rows;
    }

    private static string IndexTable(ISchemaBuilder builder, string schema)
        => builder.Dialect.QuoteForTableName(builder.TablePrefix + builder.TableNameConvention.GetIndexTable(typeof(SurveyResponseIndex), Responses), schema);

    private static bool ProbeLocalDb()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var connection = new SqlConnection(MasterConnectionString());

            connection.Open();

            return true;
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string MasterConnectionString()
        => new SqlConnectionStringBuilder
        {
            DataSource = LocalDbServer,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            Pooling = false,
            ConnectTimeout = 30,
            TrustServerCertificate = true,
        }.ConnectionString;

    private static async Task InTransactionAsync(IStore store, Func<ISchemaBuilder, Task> work)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await work(new SchemaBuilder(store.Configuration, transaction));
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed class SqliteDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private SqliteDatabase(string path, IStore store)
        {
            _path = path;
            Store = store;
        }

        public IStore Store { get; }

        public static SqliteDatabase Create(string tablePrefix = null)
        {
            var path = Path.Combine(Path.GetTempPath(), $"surveys-migrations-{Guid.NewGuid():N}.db");

            return new SqliteDatabase(path, StoreFactory.Create(configuration =>
            {
                configuration.UseSqLite($"Data Source={path};Pooling=False");

                if (tablePrefix is not null)
                {
                    configuration.SetTablePrefix(tablePrefix);
                }
            }));
        }

        public ValueTask DisposeAsync()
        {
            Store.Dispose();

            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class LocalDbDatabase : IAsyncDisposable
    {
        private readonly string _name;

        private LocalDbDatabase(string name, IStore store)
        {
            _name = name;
            Store = store;
        }

        public IStore Store { get; }

        // The tests skip through their attribute before they run (IsLocalDbAvailable), never from here.
        public static async Task<LocalDbDatabase> CreateAsync(string tablePrefix = null, string schema = null)
        {
            var name = "SurveysMigrations_" + Guid.NewGuid().ToString("N");

            await ExecuteOnMasterAsync($"CREATE DATABASE [{name}]");

            var connectionString = new SqlConnectionStringBuilder
            {
                DataSource = LocalDbServer,
                InitialCatalog = name,
                IntegratedSecurity = true,
                Pooling = false,
                TrustServerCertificate = true,
            }.ConnectionString;

            if (schema is not null)
            {
                await using DbConnection connection = new SqlConnection(connectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync($"CREATE SCHEMA [{schema}]");
            }

            return new LocalDbDatabase(name, StoreFactory.Create(configuration =>
            {
                configuration.UseSqlServer(connectionString, schema: schema);

                if (tablePrefix is not null)
                {
                    configuration.SetTablePrefix(tablePrefix);
                }
            }));
        }

        public async ValueTask DisposeAsync()
        {
            Store.Dispose();

            try
            {
                await ExecuteOnMasterAsync($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]");
            }
            catch (SqlException)
            {
            }
        }

        private static async Task ExecuteOnMasterAsync(string sql)
        {
            await using DbConnection connection = new SqlConnection(MasterConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(sql);
        }
    }
}
