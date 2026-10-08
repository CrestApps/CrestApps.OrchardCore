using System.Data.Common;
using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using Npgsql;
using YesSql;
using YesSql.Provider.PostgreSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.Migrations;

/// <summary>
/// Runs the activity index upgrade against PostgreSQL, the engine single-node production deployments run.
/// </summary>
/// <remarks>
/// The SQLite convergence gate proves the declared shapes agree, which is the root cause, but it cannot prove
/// the upgrade is executable: SQLite applies type affinity to both sides of a comparison, so a statement that
/// compares a number against a string runs there and fails on an engine that resolves operators by type. That is
/// the whole failure mode this work exists to remove — a tenant that works on SQLite and cannot activate on
/// PostgreSQL — so the upgrade is additionally executed here. Both reachable shapes of the historical schema are
/// run: the one whose enum columns are text, which the rebuild must convert, and the one whose enum columns are
/// already correct, which the rebuild must recognize and leave alone.
/// </remarks>
public sealed class OmnichannelActivityPostgresMigrationTests
{
    private const string TablePrefix = "tp_";

    [Fact]
    public async Task ActivityIndexUpgrade_FromTheTextColumnSchema_ConvertsValuesAndActivates()
    {
        var stored = await RunUpgradeAsync(useTextColumns: true);

        Assert.Equal([(int)ActivityStatus.Completed, (int)ActivityStatus.Cancelled], stored);
    }

    [Fact]
    public async Task ActivityIndexUpgrade_FromTheCorrectedEnumSchema_LeavesTheValuesUntouched()
    {
        var stored = await RunUpgradeAsync(useTextColumns: false);

        Assert.Equal([(int)ActivityStatus.Completed, (int)ActivityStatus.Cancelled], stored);
    }

    private static async Task<List<int>> RunUpgradeAsync(bool useTextColumns)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"mig{Guid.NewGuid():N}";

        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, cancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await CreateHistoricalTableAsync(schemaBuilder, useTextColumns);
                await SeedAsync(store, schemaBuilder, useTextColumns);

                var migration = new OmnichannelActivityIndexMigrations(store)
                {
                    SchemaBuilder = schemaBuilder,
                };

                var version = await migration.UpdateFrom4Async();

                Assert.Equal(5, version);

                await transaction.CommitAsync(cancellationToken);
            }

            return await ReadStatusesAsync(store, connectionString, cancellationToken);
        }
        finally
        {
            store.Dispose();
            await ExecuteAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cancellationToken);
        }
    }

    private static async Task CreateHistoricalTableAsync(SchemaBuilder schemaBuilder, bool useTextColumns)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Source", column => column.WithLength(50))
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("ChannelEndpointId", column => column.WithLength(26))
                .Column<string>("PreferredDestination", column => column.WithLength(255))
                .Column<string>("AIProfileName", column => column.WithLength(255))
                .Column<string>("ContactContentItemId", column => column.WithLength(26))
                .Column<string>("ContactContentType", column => column.WithLength(255))
                .Column<string>("CampaignId", column => column.WithLength(26))
                .Column<string>("SubjectContentType", column => column.WithLength(26))
                .Column<DateTime>("ScheduledUtc", column => column.NotNull())
                .Column<DateTime>("CompletedUtc")
                .Column<int>("Attempts", column => column.NotNull())
                .Column<string>("AssignedToId", column => column.WithLength(26))
                .Column<DateTime>("AssignedToUtc")
                .Column<string>("ReservationId", column => column.WithLength(26))
                .Column<string>("ReservedById", column => column.WithLength(26))
                .Column<DateTime>("ReservedUtc")
                .Column<DateTime>("ReservationExpiresUtc")
                .Column<string>("CreatedById", column => column.WithLength(26))
                .Column<string>("DispositionId", column => column.WithLength(26))
                .Column<DateTime>("CreatedUtc", column => column.NotNull());

            if (useTextColumns)
            {
                table
                    .Column<string>("Kind", column => column.WithLength(50))
                    .Column<string>("AssignmentStatus", column => column.WithLength(50))
                    .Column<string>("UrgencyLevel", column => column.WithLength(50))
                    .Column<string>("Status", column => column.WithLength(50))
                    .Column<string>("InteractionType", column => column.WithLength(50));

                return;
            }

            table
                .Column<ActivityKind>("Kind")
                .Column<ActivityAssignmentStatus>("AssignmentStatus")
                .Column<ActivityUrgencyLevel>("UrgencyLevel")
                .Column<ActivityStatus>("Status")
                .Column<ActivityInteractionType>("InteractionType");
        },
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "AssignmentStatus",
                "InteractionType",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
                "ContactContentType",
                "ContactContentItemId",
                "Status",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
                "AssignmentStatus",
                "ReservationId",
                "ReservedById",
                "ScheduledUtc",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);
    }

    private static async Task SeedAsync(IStore store, SchemaBuilder schemaBuilder, bool useTextColumns)
    {
        var quotedTableName = QuotedTableName(store);

        // A text column holds whatever the engine made of the integer YesSql wrote through it, so the number as
        // text is what a real upgraded tenant carries.
        object completed = useTextColumns
            ? ((int)ActivityStatus.Completed).ToString(CultureInfo.InvariantCulture)
            : (int)ActivityStatus.Completed;
        object cancelled = useTextColumns
            ? ((int)ActivityStatus.Cancelled).ToString(CultureInfo.InvariantCulture)
            : (int)ActivityStatus.Cancelled;

        var quotedDocumentTableName = QuotedDocumentTableName(store);

        await InsertDocumentAsync(schemaBuilder, quotedDocumentTableName, 1);
        await InsertDocumentAsync(schemaBuilder, quotedDocumentTableName, 2);

        await InsertAsync(schemaBuilder, quotedTableName, 1, "activity-1", completed);
        await InsertAsync(schemaBuilder, quotedTableName, 2, "activity-2", cancelled);
    }

    private static async Task InsertDocumentAsync(
        SchemaBuilder schemaBuilder,
        string quotedDocumentTableName,
        long documentId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            $"""
            INSERT INTO {quotedDocumentTableName} ("Id", "Type", "Content", "Version")
            VALUES (@Id, @Type, @Content, 1)
            """;

        AddParameter(command, "@Id", documentId);
        AddParameter(command, "@Type", typeof(OmnichannelActivity).FullName);
        AddParameter(command, "@Content", "{}");

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAsync(
        SchemaBuilder schemaBuilder,
        string quotedTableName,
        long documentId,
        string itemId,
        object status)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            $"""
            INSERT INTO {quotedTableName} ("DocumentId", "ItemId", "Status", "ScheduledUtc", "CreatedUtc", "Attempts")
            VALUES (@DocumentId, @ItemId, @Status, @ScheduledUtc, @CreatedUtc, 0)
            """;

        AddParameter(command, "@DocumentId", documentId);
        AddParameter(command, "@ItemId", itemId);
        AddParameter(command, "@Status", status);
        AddParameter(command, "@ScheduledUtc", DateTime.UnixEpoch);
        AddParameter(command, "@CreatedUtc", DateTime.UnixEpoch);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<int>> ReadStatusesAsync(
        IStore store,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var statuses = new List<int>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Status\" FROM {QuotedTableName(store)} ORDER BY \"DocumentId\"";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            statuses.Add(reader.GetInt32(0));
        }

        return statuses;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string QuotedTableName(IStore store)
    {
        var tableName = store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelActivityIndex), OmnichannelConstants.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }

    private static string QuotedDocumentTableName(IStore store)
    {
        var tableName = store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetDocumentTable(OmnichannelConstants.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }

    private static IStore CreateStore(string connectionString, string schema)
        => StoreFactory.Create(configuration =>
        {
            configuration.UsePostgreSql(connectionString);
            configuration.Schema = schema;

            // A named schema and a table prefix are independent tenant options, and it is their combination that
            // exposes an index drop naming an index that was never created, so both are set here.
            configuration.TablePrefix = TablePrefix;
        });

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_CENTER_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CONTACT_CENTER_POSTGRES_CONNECTION must point to the PostgreSQL instance used by the Contact Center migration safety gate.");
        }

        return connectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
