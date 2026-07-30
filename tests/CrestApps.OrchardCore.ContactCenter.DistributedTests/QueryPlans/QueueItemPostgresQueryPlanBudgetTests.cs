using System.Data.Common;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using Npgsql;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.PostgreSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.QueryPlans;

/// <summary>
/// Asserts the agent workspace's query-plan budget on PostgreSQL, the engine single-node production deployments
/// run. SQLite's planner is not PostgreSQL's: a statement that seeks an index on one can sequentially scan on
/// the other, and the query returns identical results either way, so only the plan on the deployed engine is
/// evidence. Every signed-in agent polls this statement continuously, so a sequential scan makes the cost of a
/// poll grow with everything the contact center has ever enqueued.
/// </summary>
public sealed class QueueItemPostgresQueryPlanBudgetTests
{
    private const int SeededQueueItems = 40000;
    private const int SeededQueues = 400;

    [Fact]
    public async Task WaitingCountByQueue_DoesNotSequentiallyScanTheQueueItemTable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"qq{Guid.NewGuid():N}";
        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store, store.Configuration, seedTransaction);
                await SeedAsync(store.Configuration, seedTransaction, cancellationToken);
                await seedTransaction.CommitAsync(cancellationToken);
            }

            // The planner chooses from statistics, so a table it has never looked at is a table it will guess
            // about. Analyzing first makes the assertion a statement about the query rather than about timing.
            await ExecuteAsync(
                connectionString,
                $"ANALYZE \"{schema}\".\"{TableName(store.Configuration)}\";",
                cancellationToken);

            // Act
            var plan = await ExplainAsync(store, cancellationToken);

            // Assert
            var tableName = TableName(store.Configuration);
            var rendered = string.Join(Environment.NewLine, plan);

            Assert.DoesNotContain(
                plan,
                line => line.Contains("Seq Scan", StringComparison.OrdinalIgnoreCase)
                    && line.Contains(tableName, StringComparison.OrdinalIgnoreCase));

            // Without the covering index the planner does not necessarily fall back to a sequential scan: the
            // retention index leads with Status, so it can seek that and walk every waiting item in the tenant
            // for each queue asked about. That plan is the regression this budget exists to reject, so no other
            // index may answer this question either.
            Assert.DoesNotContain(
                plan,
                line => line.Contains("IDX_QueueItemIndex_Retention", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                plan.Any(line => line.Contains("IDX_QueueItemIndex_WaitingByQueue", StringComparison.OrdinalIgnoreCase)),
                $"The workspace poll must seek IDX_QueueItemIndex_WaitingByQueue. Plan:{Environment.NewLine}{rendered}");
        }
        finally
        {
            store.Dispose();
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cancellationToken);
        }
    }

    [Fact]
    public async Task WaitingCountByQueue_ExecutesAndCountsCorrectly_OnPostgreSql()
    {
        // A plan is not proof that a statement runs. Providers differ in how a bound collection reaches the
        // server, and on an engine that binds a collection as one value an "IN" against it is not valid syntax
        // at all — the statement fails outright while an EXPLAIN of a hand-expanded variant looks healthy. Only
        // executing the statement the store sends, on the engine production deploys to, distinguishes the two.
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"qw{Guid.NewGuid():N}";
        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);
            store.RegisterIndexes([new QueueItemIndexProvider()], ContactCenterConstants.CollectionName);

            await using (var schemaSession = store.CreateSession())
            {
                var schemaTransaction = await schemaSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store, store.Configuration, schemaTransaction);
                await schemaTransaction.CommitAsync(cancellationToken);
            }

            await using (var seedSession = store.CreateSession())
            {
                await SaveAsync(seedSession, "busy-one", "queue-busy", QueueItemStatus.Waiting, cancellationToken);
                await SaveAsync(seedSession, "busy-two", "queue-busy", QueueItemStatus.Waiting, cancellationToken);
                await SaveAsync(seedSession, "busy-done", "queue-busy", QueueItemStatus.Completed, cancellationToken);
                await SaveAsync(seedSession, "quiet-one", "queue-quiet", QueueItemStatus.Waiting, cancellationToken);
                await seedSession.SaveChangesAsync(cancellationToken);
            }

            // Act
            await using var querySession = store.CreateSession();
            var queueItemStore = new QueueItemStore(querySession);
            var counts = await queueItemStore.CountWaitingByQueueIdsAsync(
                ["queue-busy", "queue-quiet", "queue-empty"],
                cancellationToken);

            // Assert
            Assert.Equal(2, counts["queue-busy"]);
            Assert.Equal(1, counts["queue-quiet"]);
            Assert.False(counts.ContainsKey("queue-empty"));
        }
        finally
        {
            store.Dispose();
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cancellationToken);
        }
    }

    private static async Task SaveAsync(
        ISession session,
        string itemId,
        string queueId,
        QueueItemStatus status,
        CancellationToken cancellationToken)
    {
        await session.SaveAsync(
            new QueueItem
            {
                ItemId = itemId,
                QueueId = queueId,
                ActivityItemId = $"activity-{itemId}",
                EnqueuedUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc),
            }.RestorePersistedStatus(status),
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: cancellationToken);
    }

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_CENTER_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CONTACT_CENTER_POSTGRES_CONNECTION must point to the PostgreSQL instance used by the Contact Center query-plan budget gate.");
        }

        return connectionString;
    }

    private static IStore CreateStore(string connectionString, string schema)
        => StoreFactory.Create(configuration =>
        {
            configuration.UsePostgreSql(connectionString);
            configuration.Schema = schema;
        });

    private static string TableName(IConfiguration configuration)
        => configuration.TableNameConvention.GetIndexTable(typeof(QueueItemIndex), ContactCenterConstants.CollectionName);

    private static async Task MigrateAsync(IStore store, IConfiguration configuration, DbTransaction transaction)
    {
        // The real migrations are what production runs, so the plan is measured against the schema the product
        // ships rather than a hand-written copy carrying indexes nobody deploys.
        var migration = new QueueItemIndexMigrations(store, CreateClock())
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom3Async();
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));

        return clock.Object;
    }

    private static async Task SeedAsync(IConfiguration configuration, DbTransaction transaction, CancellationToken cancellationToken)
    {
        // A planner reads a table end to end when the table is small enough that doing so is genuinely cheaper,
        // and when the rows it wants are a large enough share of the table that seeking each one costs more than
        // one pass. A budget seeded with a handful of rows, or with the work of only a few queues, therefore
        // passes no matter how the statement is written.
        var dialect = configuration.SqlDialect;
        var tableName = dialect.QuoteForTableName(TableName(configuration), configuration.Schema);
        var statusCount = Enum.GetValues<QueueItemStatus>().Length;

        // PostgreSQL enforces the index table's foreign key back to the document table, so the documents the
        // index rows belong to have to exist. Seeding only the index would prove a plan for a shape the database
        // would never have accepted.
        var documentTable = dialect.QuoteForTableName(
            configuration.TableNameConvention.GetDocumentTable(ContactCenterConstants.CollectionName),
            configuration.Schema);

        await ExecuteAsync(
            transaction,
            $"INSERT INTO {documentTable} (" +
            $"{dialect.QuoteForColumnName("Id")}, " +
            $"{dialect.QuoteForColumnName("Type")}, " +
            $"{dialect.QuoteForColumnName("Content")}, " +
            $"{dialect.QuoteForColumnName("Version")}) " +
            $"SELECT series, '{typeof(QueueItem).FullName}', '{{}}', 1 " +
            $"FROM generate_series(1, {SeededQueueItems}) AS series",
            cancellationToken);

        // The status must not be a function of the queue alone. Deriving both from the same series without the
        // offset would leave every row of a given queue on the same status, so the sampled queues would hold no
        // waiting items and the planner would be answering a question with no rows behind it.
        await ExecuteAsync(
            transaction,
            $"INSERT INTO {tableName} (" +
            $"{dialect.QuoteForColumnName("DocumentId")}, " +
            $"{dialect.QuoteForColumnName("ItemId")}, " +
            $"{dialect.QuoteForColumnName("QueueId")}, " +
            $"{dialect.QuoteForColumnName("ActivityItemId")}, " +
            $"{dialect.QuoteForColumnName("ActivityClaimKey")}, " +
            $"{dialect.QuoteForColumnName("Status")}, " +
            $"{dialect.QuoteForColumnName("Priority")}, " +
            $"{dialect.QuoteForColumnName("EnqueuedUtc")}) " +
            "SELECT series, " +
            "'queue-item-' || LPAD(series::text, 6, '0'), " +
            $"'queue-' || LPAD((series % {SeededQueues})::text, 4, '0'), " +
            "'activity-' || LPAD(series::text, 6, '0'), " +
            "'claim-' || LPAD(series::text, 6, '0'), " +
            $"(((series % {SeededQueues}) + (series / {SeededQueues})) % {statusCount}), " +
            $"{(int)InteractionPriority.Normal}, " +
            "TIMESTAMP '2026-07-16 12:00:00' + (series * INTERVAL '1 second') " +
            $"FROM generate_series(1, {SeededQueueItems}) AS series",
            cancellationToken);
    }

    private static async Task ExecuteAsync(DbTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(IStore store, CancellationToken cancellationToken)
    {
        // The statement measured is the statement the store executes, read from the same builder, so the gate
        // cannot drift into proving a plan for a query only the gate runs.
        var queueIds = Enumerable.Range(0, 5).Select(index => $"queue-{index:D4}").ToArray();
        var sql = QueueItemQueries.BuildWaitingCountByQueueSql(store.Configuration, queueIds.Length);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;

        // The statement is measured verbatim. Rewriting it here — expanding a bound collection into placeholders,
        // or into a set-returning function — would mean the plan describes a statement the store never sends,
        // which is how a query that cannot execute at all on this engine can still be reported as well planned
        // on it. It also changes the plan itself: the planner cannot estimate how many rows a set-returning
        // function yields and falls back to a sequential scan.
        command.CommandText = "EXPLAIN (FORMAT JSON) " + sql;

        for (var index = 0; index < queueIds.Length; index++)
        {
            AddParameter(command, QueueItemQueries.QueueIdParameterName(index)).Value = queueIds[index];
        }

        var lines = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.AddRange(Flatten(reader.GetString(0)));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        Assert.NotEmpty(lines);

        return lines;
    }

    private static List<string> Flatten(string json)
    {
        using var document = JsonDocument.Parse(json);
        var lines = new List<string>();

        Walk(document.RootElement, lines);

        return lines;
    }

    private static void Walk(JsonElement element, List<string> lines)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, lines);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("Node Type", out var nodeType))
        {
            var relation = element.TryGetProperty("Relation Name", out var relationName)
                ? relationName.GetString()
                : string.Empty;

            var indexName = element.TryGetProperty("Index Name", out var index)
                ? index.GetString()
                : string.Empty;

            lines.Add($"{nodeType.GetString()} {relation} {indexName}".Trim());
        }

        foreach (var property in element.EnumerateObject())
        {
            Walk(property.Value, lines);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbParameter AddParameter(DbCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        command.Parameters.Add(parameter);

        return parameter;
    }
}
