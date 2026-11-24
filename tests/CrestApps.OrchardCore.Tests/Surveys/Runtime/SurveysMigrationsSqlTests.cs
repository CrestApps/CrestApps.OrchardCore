using System.Data.Common;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
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
    public async Task UpdateFrom5_OnSqlite_AddsTheSurveyListColumnsAndTagIndex_AndAsksForTheReindexStep()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom5Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom5_OnSqlServer_AddsTheSurveyListColumnsAndTagIndex_AndAsksForTheReindexStep()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        await AssertUpdateFrom5Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom6_OnSqlite_AddsTheReminderColumns_ExistingRowsReadNoneSentAndNotStopped_AndAsksForTheStep()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom6Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom6_OnSqlServer_AddsTheReminderColumns_ExistingRowsReadNoneSentAndNotStopped_AndAsksForTheStep()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        await AssertUpdateFrom6Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom7_OnSqlite_RecordsWhenTheCloseStepStarted()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom7Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom7_OnSqlServer_RecordsWhenTheCloseStepStarted()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        await AssertUpdateFrom7Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom8_OnSqlite_AddsTheSurveyUpkeepColumns_AndAsksForTheUpkeepReindexStep()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom8Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom8_OnSqlServer_AddsTheSurveyUpkeepColumns_AndAsksForTheUpkeepReindexStep()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        await AssertUpdateFrom8Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom9_OnSqlite_CreatesTheOptOutIndex_AndAsksForTheMoveStep()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom9Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom9_OnSqlServerWithATablePrefixAndASchema_CreatesTheOptOutIndex_AndAsksForTheMoveStep()
    {
        await using var database = await LocalDbDatabase.CreateAsync(TablePrefix, Schema);

        await AssertUpdateFrom9Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom10_OnSqlite_AddsTheVersionListColumns_ExistingRowsReadEmpty_AndAsksForTheReindexStep()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom10Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom10_OnSqlServer_AddsTheVersionListColumns_ExistingRowsReadEmpty_AndAsksForTheReindexStep()
    {
        await using var database = await LocalDbDatabase.CreateAsync();

        await AssertUpdateFrom10Async(database.Store);
    }

    [Fact]
    public async Task UpdateFrom11_OnSqlite_AddsTheInvitationRecipientAndSendIndexes()
    {
        await using var database = SqliteDatabase.Create();

        await AssertUpdateFrom11Async(database.Store);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task UpdateFrom11_OnSqlServerWithATablePrefixAndASchema_AddsTheInvitationRecipientAndSendIndexes()
    {
        await using var database = await LocalDbDatabase.CreateAsync(TablePrefix, Schema);

        await AssertUpdateFrom11Async(database.Store);
    }

    [Fact]
    public async Task CreateAsync_OnSqlite_MakesTheSameSchemaAsTheUpdatesFromVersion1()
    {
        // Arrange: one site made with version 1 and upgraded step by step, one made now.
        await using var upgraded = SqliteDatabase.Create();
        await using var created = SqliteDatabase.Create();
        var finalVersion = 0;
        var createdVersion = 0;

        await InitializeAsync(upgraded.Store);
        await InitializeAsync(created.Store);
        await InTransactionAsync(upgraded.Store, SurveysVersion1Schema.CreateAsync);

        // Act
        for (var from = 1; from < 100; from++)
        {
            var update = typeof(SurveysMigrations).GetMethod($"UpdateFrom{from}Async");

            if (update is null)
            {
                break;
            }

            await InTransactionAsync(upgraded.Store, async builder => finalVersion = await (Task<int>)update.Invoke(Migration(upgraded.Store, builder), null));
            Assert.Equal(from + 1, finalVersion);
        }

        await InTransactionAsync(created.Store, async builder => createdVersion = await Migration(created.Store, builder).CreateAsync());

        // Assert: the same version, tables, columns and indexes.
        Assert.Equal(createdVersion, finalVersion);
        Assert.Equal(await DescribeSqliteSchemaAsync(created.Store), await DescribeSqliteSchemaAsync(upgraded.Store));
    }

    private static async Task AssertUpdateFrom5Async(IStore store)
    {
        // Arrange: the survey index as version 5 made it, with a row in it.
        var pending = new ResumableStepsDocument();

        await InitializeAsync(store);
        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("OwnerRef", column => column.Nullable().WithLength(255))
            .Column<int>("PublishedVersionNumber")
            .Column<DateTime?>("ModifiedUtc", column => column.Nullable()),
            collection: SurveysConstants.CollectionName));
        await InsertRowAsync(store, typeof(SurveyIndex), ("ItemId", "s1"), ("PublishedVersionNumber", 0));

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder, pending).UpdateFrom5Async());

        // Assert
        Assert.Equal(6, version);
        Assert.True(pending.Pending.ContainsKey(SurveysReindexStep.StepName));

        var row = await ReadRowAsync(store, typeof(SurveyIndex), "OwnerId", "Anonymity", "MinimumGroupSize");

        // The existing row reads no anonymity, so readers take it from the survey until the step indexes it again.
        Assert.Null(row["OwnerId"]);
        Assert.Null(row["Anonymity"]);
        Assert.Null(row["MinimumGroupSize"]);
        Assert.Equal(0, await CountRowsAsync(store, typeof(SurveyTagIndex)));
    }

    private static async Task AssertUpdateFrom6Async(IStore store)
    {
        // Arrange: the invitation index as version 6 made it, with a row in it.
        var pending = new ResumableStepsDocument();

        await InitializeAsync(store);
        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyInvitationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CollectorId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<bool>("Held", column => column.NotNull().WithDefault(false)),
            collection: SurveysConstants.CollectionName));
        await InsertRowAsync(store, typeof(SurveyInvitationIndex), ("ItemId", "i1"), ("Status", "Sent"));

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder, pending).UpdateFrom6Async());

        // Assert
        Assert.Equal(7, version);
        Assert.True(pending.Pending.ContainsKey(SurveyInvitationRemindersStep.StepName));

        var row = await ReadRowAsync(store, typeof(SurveyInvitationIndex), "RemindersSent", "RemindersStopped");

        Assert.Equal(0L, Convert.ToInt64(row["RemindersSent"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(row["RemindersStopped"], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task AssertUpdateFrom7Async(IStore store)
    {
        // Arrange
        var startedUtc = new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc);
        var clock = new Mock<global::OrchardCore.Modules.IClock>();
        clock.SetupGet(item => item.UtcNow).Returns(startedUtc);

        await InitializeAsync(store);

        // Act
        var version = 0;

        await using (var session = store.CreateSession())
        {
            var migration = new SurveysMigrations(Mock.Of<IRecipeMigrator>(), new ResumableSteps(Mock.Of<IDocumentManager<ResumableStepsDocument>>()), store, session, clock.Object);

            version = await migration.UpdateFrom7Async();
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var read = store.CreateSession();
        var start = await read.Query<SurveyCloseStepStart>(SurveysConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.Equal(8, version);
        Assert.NotNull(start);
        Assert.Equal(startedUtc, DateTime.SpecifyKind(start.StartedUtc, DateTimeKind.Utc));
    }

    private static async Task AssertUpdateFrom8Async(IStore store)
    {
        // Arrange: the survey index as version 8 made it, with a row in it.
        var pending = new ResumableStepsDocument();

        await InitializeAsync(store);
        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("OwnerRef", column => column.Nullable().WithLength(255))
            .Column<int>("PublishedVersionNumber")
            .Column<DateTime?>("ModifiedUtc", column => column.Nullable())
            .Column<string>("OwnerId", column => column.Nullable().WithLength(26))
            .Column<string>("Anonymity", column => column.Nullable().WithLength(20))
            .Column<int?>("MinimumGroupSize", column => column.Nullable()),
            collection: SurveysConstants.CollectionName));
        await InsertRowAsync(store, typeof(SurveyIndex), ("ItemId", "s1"), ("PublishedVersionNumber", 0));

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder, pending).UpdateFrom8Async());

        // Assert
        Assert.Equal(9, version);
        Assert.True(pending.Pending.ContainsKey(SurveyUpkeepReindexStep.StepName));

        var row = await ReadRowAsync(store, typeof(SurveyIndex), "RetentionDays", "ClosesUtc", "TotalLimit");

        // The existing row reads no retention days, so upkeep reads the survey itself until the step indexes it again.
        Assert.Null(row["RetentionDays"]);
        Assert.Null(row["ClosesUtc"]);
        Assert.Null(row["TotalLimit"]);
    }

    private static async Task AssertUpdateFrom9Async(IStore store)
    {
        // Arrange: a site of version 9 has no opt-out index.
        var pending = new ResumableStepsDocument();

        await InitializeAsync(store);

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder, pending).UpdateFrom9Async());

        // Assert: the table takes a record, under the tenant's prefix (and schema), with its index.
        Assert.Equal(10, version);
        Assert.True(pending.Pending.ContainsKey(SurveyInvitationOptOutMoveStep.StepName));

        await InsertRowAsync(store, typeof(SurveyInvitationOptOutIndex), ("AddressHash", new string('a', 64)));

        Assert.Equal(1, await CountRowsAsync(store, typeof(SurveyInvitationOptOutIndex)));
        Assert.True(await IndexExistsAsync(store, "IDX_SurveyInvitationOptOutIndex_Hash"));
    }

    private static async Task AssertUpdateFrom10Async(IStore store)
    {
        // Arrange: the version index as version 10 made it, with a row in it.
        var pending = new ResumableStepsDocument();

        await InitializeAsync(store);
        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyVersionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<int>("Number")
            .Column<DateTime>("PublishedUtc"),
            collection: SurveysConstants.CollectionName));
        await InsertRowAsync(store, typeof(SurveyVersionIndex), ("ItemId", "v1"), ("SurveyId", "s1"), ("Number", 1), ("PublishedUtc", new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc)));

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder, pending).UpdateFrom10Async());

        // Assert: the existing row reads no publisher and no notes, so the list reads the version until the step
        // indexes it again; a note as long as the column allows fits.
        Assert.Equal(11, version);
        Assert.True(pending.Pending.ContainsKey(SurveyVersionsReindexStep.StepName));

        var row = await ReadRowAsync(store, typeof(SurveyVersionIndex), "PublishedBy", "Notes");

        Assert.Null(row["PublishedBy"]);
        Assert.Null(row["Notes"]);

        await InsertRowAsync(store, typeof(SurveyVersionIndex), ("ItemId", "v2"), ("SurveyId", "s1"), ("Number", 2), ("PublishedUtc", new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc)), ("PublishedBy", "u1"), ("Notes", new string('n', SurveyVersionIndex.NotesLength)));

        Assert.Equal(2, await CountRowsAsync(store, typeof(SurveyVersionIndex)));
    }

    private static async Task AssertUpdateFrom11Async(IStore store)
    {
        // Arrange: the invitation index as version 11 made it.
        await InitializeAsync(store);
        await InTransactionAsync(store, builder => builder.CreateMapIndexTableAsync<SurveyInvitationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CollectorId", column => column.Nullable().WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("RecipientRef", column => column.Nullable().WithLength(255))
            .Column<DateTime?>("LastReminderUtc", column => column.Nullable())
            .Column<DateTime?>("SentUtc", column => column.Nullable()),
            collection: SurveysConstants.CollectionName));

        // Act
        var version = 0;

        await InTransactionAsync(store, async builder => version = await Migration(store, builder).UpdateFrom11Async());

        // Assert
        Assert.Equal(12, version);

        foreach (var name in new[] { "IDX_SurveyInvitationIndex_User", "IDX_SurveyInvitationIndex_Recipient", "IDX_SurveyInvitationIndex_Sent", "IDX_SurveyInvitationIndex_Reminded" })
        {
            Assert.True(await IndexExistsAsync(store, name), name);
        }
    }

    private static SurveysMigrations Migration(IStore store, ISchemaBuilder builder, ResumableStepsDocument pending = null)
    {
        var documents = new Mock<IDocumentManager<ResumableStepsDocument>>();
        documents.Setup(manager => manager.GetOrCreateMutableAsync(It.IsAny<Func<Task<ResumableStepsDocument>>>())).ReturnsAsync(pending ?? new ResumableStepsDocument());

        return new SurveysMigrations(Mock.Of<IRecipeMigrator>(), new ResumableSteps(documents.Object), store, Mock.Of<ISession>(), Mock.Of<global::OrchardCore.Modules.IClock>())
        {
            SchemaBuilder = builder,
        };
    }

    private static async Task InitializeAsync(IStore store)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(Responses, cancellationToken);
    }

    // Writes an index row as an earlier version's provider would have, for a document that exists (SQL Server checks the
    // row's document).
    private static async Task InsertRowAsync(IStore store, Type index, params (string Column, object Value)[] values)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = store.CreateSession())
        {
            await session.SaveAsync(new SurveyInvitation { ItemId = IdGenerator.GenerateId() }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await InTransactionAsync(store, async builder =>
        {
            var dialect = builder.Dialect;
            var configuration = store.Configuration;
            var documents = dialect.QuoteForTableName(builder.TablePrefix + builder.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName), configuration.Schema);
            var columns = string.Join(", ", values.Select(value => dialect.QuoteForColumnName(value.Column)));
            var parameters = new DynamicParameters();

            for (var i = 0; i < values.Length; i++)
            {
                parameters.Add("p" + i, values[i].Value);
            }

            await builder.Connection.ExecuteAsync(
                $"INSERT INTO {Table(builder, store, index)} ({dialect.QuoteForColumnName("DocumentId")}, {columns}) " +
                $"SELECT MAX({dialect.QuoteForColumnName("Id")}), {string.Join(", ", values.Select((_, i) => "@p" + i))} FROM {documents}",
                parameters,
                builder.Transaction);
        });
    }

    private static async Task<IDictionary<string, object>> ReadRowAsync(IStore store, Type index, params string[] columns)
    {
        IDictionary<string, object> row = null;

        await InTransactionAsync(store, async builder =>
        {
            var dialect = builder.Dialect;

            row = (IDictionary<string, object>)await builder.Connection.QuerySingleAsync(
                $"SELECT {string.Join(", ", columns.Select(column => $"{dialect.QuoteForColumnName(column)} AS {column}"))} FROM {Table(builder, store, index)}",
                transaction: builder.Transaction);
        });

        return row;
    }

    private static async Task<long> CountRowsAsync(IStore store, Type index)
    {
        long count = 0;

        await InTransactionAsync(store, async builder => count = await builder.Connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {Table(builder, store, index)}", transaction: builder.Transaction));

        return count;
    }

    // Whether a database index whose name ends with the given name exists (YesSql may prefix index names).
    private static async Task<bool> IndexExistsAsync(IStore store, string name)
    {
        var found = 0L;

        await InTransactionAsync(store, async builder =>
        {
            var sql = store.Configuration.SqlDialect is SqlServerDialect
                ? "SELECT COUNT(*) FROM sys.indexes WHERE name LIKE @Name"
                : "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE @Name";

            found = await builder.Connection.ExecuteScalarAsync<long>(sql, new { Name = "%" + name }, builder.Transaction);
        });

        return found > 0;
    }

    private static string Table(ISchemaBuilder builder, IStore store, Type index)
        => builder.Dialect.QuoteForTableName(builder.TablePrefix + builder.TableNameConvention.GetIndexTable(index, index == typeof(SurveyResponseIndex) ? Responses : SurveysConstants.CollectionName), store.Configuration.Schema);

    // Every table of a SQLite database with its columns (name, type, not null, default, key) and indexes (name, unique,
    // columns), sorted, so two schemas compare whatever order their columns were added in.
    private static async Task<List<string>> DescribeSqliteSchemaAsync(IStore store)
    {
        var lines = new List<string>();

        await InTransactionAsync(store, async builder =>
        {
            var connection = builder.Connection;
            var transaction = builder.Transaction;
            var tables = (await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'", transaction: transaction)).ToList();

            foreach (var table in tables)
            {
                foreach (var column in await connection.QueryAsync($"PRAGMA table_info('{table}')", transaction: transaction))
                {
                    var values = (IDictionary<string, object>)column;

                    lines.Add(string.Join('|', table, values["name"], values["type"], values["notnull"], Convert.ToString(values["dflt_value"], System.Globalization.CultureInfo.InvariantCulture)?.ToUpperInvariant(), values["pk"]));
                }

                foreach (var index in await connection.QueryAsync($"PRAGMA index_list('{table}')", transaction: transaction))
                {
                    var values = (IDictionary<string, object>)index;
                    var name = (string)values["name"];
                    var columns = (await connection.QueryAsync($"PRAGMA index_info('{name}')", transaction: transaction))
                        .Select(item => (IDictionary<string, object>)item)
                        .OrderBy(item => Convert.ToInt64(item["seqno"], System.Globalization.CultureInfo.InvariantCulture))
                        .Select(item => (string)item["name"]);

                    lines.Add(string.Join('|', table, "index", name, values["unique"], string.Join(',', columns)));
                }
            }
        });

        lines.Sort(StringComparer.Ordinal);

        return lines;
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
            var migration = new SurveysMigrations(Mock.Of<IRecipeMigrator>(), new ResumableSteps(Mock.Of<IDocumentManager<ResumableStepsDocument>>()), store, Mock.Of<ISession>(), Mock.Of<global::OrchardCore.Modules.IClock>())
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
