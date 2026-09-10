using System.Data.Common;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Npgsql;
using YesSql;
using YesSql.Provider.PostgreSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.QueryPlans;

/// <summary>
/// Asserts the reservation path's query-plan budget on PostgreSQL, the engine single-node production deployments
/// run. SQLite's planner is not PostgreSQL's: a statement that seeks an index on one can sequentially scan on the
/// other, and the query returns identical results either way, so only the plan on the deployed engine is
/// evidence. Routing runs this statement before every offer, so a sequential scan makes the cost of a routing
/// decision grow with everything the contact center has ever recorded.
/// </summary>
public sealed class InteractionPostgresQueryPlanBudgetTests
{
    private const int SeededInteractions = 40000;
    private const int SeededAgents = 400;

    [Fact]
    public async Task ActiveCountByAgent_DoesNotSequentiallyScanTheInteractionTable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"qp{Guid.NewGuid():N}";
        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store.Configuration, seedTransaction);
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

            Assert.True(
                plan.Any(line => line.Contains("IDX_InteractionIndex_ActiveByAgent", StringComparison.OrdinalIgnoreCase)),
                $"The reservation path must seek IDX_InteractionIndex_ActiveByAgent. Plan:{Environment.NewLine}{rendered}");
        }
        finally
        {
            store.Dispose();
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cancellationToken);
        }
    }

    [Fact]
    public async Task ActiveCountByAgent_ExecutesAndCountsCorrectly_OnPostgreSql()
    {
        // A plan is not proof that a statement runs. Providers differ in how a bound collection reaches the
        // server, and on an engine that binds a collection as one value an "IN" against it is not valid syntax
        // at all — the statement fails outright while an EXPLAIN of a hand-expanded variant looks healthy. Only
        // executing the statement the store sends, on the engine production deploys to, distinguishes the two.
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"qx{Guid.NewGuid():N}";
        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);
            store.RegisterIndexes([new InteractionIndexProvider()]);

            await using (var schemaSession = store.CreateSession())
            {
                var schemaTransaction = await schemaSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store.Configuration, schemaTransaction);
                await schemaTransaction.CommitAsync(cancellationToken);
            }

            await using (var seedSession = store.CreateSession())
            {
                await SaveAsync(seedSession, "busy-one", "agent-busy", InteractionStatus.Connected, cancellationToken);
                await SaveAsync(seedSession, "busy-two", "agent-busy", InteractionStatus.Held, cancellationToken);
                await SaveAsync(seedSession, "settled", "agent-busy", InteractionStatus.Ended, cancellationToken);
                await SaveAsync(seedSession, "pending", "agent-busy", InteractionStatus.Created, cancellationToken);
                await SaveAsync(seedSession, "other", "agent-quiet", InteractionStatus.Ringing, cancellationToken);
                await seedSession.SaveChangesAsync(cancellationToken);
            }

            // Act
            await using var querySession = store.CreateSession();
            var interactionStore = new InteractionStore(querySession);
            var counts = await interactionStore.CountActiveByAgentIdsAsync(
                ["agent-busy", "agent-quiet", "agent-idle"],
                cancellationToken);

            // Assert
            Assert.Equal(2, counts["agent-busy"]);
            Assert.Equal(1, counts["agent-quiet"]);
            Assert.False(counts.ContainsKey("agent-idle"));
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
        string agentId,
        InteractionStatus status,
        CancellationToken cancellationToken)
    {
        await session.SaveAsync(
            new Interaction
            {
                ItemId = itemId,
                AgentId = agentId,
                CreatedUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc),
            }.RestorePersistedStatus(status),
            collection: ContactCenterStorage.CollectionName,
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
        => configuration.TableNameConvention.GetIndexTable(typeof(InteractionIndex), ContactCenterStorage.CollectionName);

    private static async Task MigrateAsync(IConfiguration configuration, DbTransaction transaction)
    {
        // The real migrations are what production runs, so the plan is measured against the schema the product
        // ships rather than a hand-written copy carrying indexes nobody deploys.
        var migration = new InteractionIndexMigrations
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom1Async();
        await migration.UpdateFrom2Async();
        await migration.UpdateFrom3Async();
        await migration.UpdateFrom4Async();
        await migration.UpdateFrom5Async();
    }

    private static async Task SeedAsync(IConfiguration configuration, DbTransaction transaction, CancellationToken cancellationToken)
    {
        // A planner reads a table end to end when the table is small enough that doing so is genuinely cheaper,
        // and when the rows it wants are a large enough share of the table that seeking each one costs more than
        // one pass. A budget seeded with a handful of rows, or with the work of only a few agents, therefore
        // passes no matter how the statement is written.
        var dialect = configuration.SqlDialect;
        var tableName = dialect.QuoteForTableName(TableName(configuration), configuration.Schema);
        var statusCount = Enum.GetValues<InteractionStatus>().Length;

        // PostgreSQL enforces the index table's foreign key back to the document table, so the documents the
        // index rows belong to have to exist. Seeding only the index would prove a plan for a shape the database
        // would never have accepted.
        var documentTable = dialect.QuoteForTableName(
            configuration.TableNameConvention.GetDocumentTable(ContactCenterStorage.CollectionName),
            configuration.Schema);

        await ExecuteAsync(
            transaction,
            $"INSERT INTO {documentTable} (" +
            $"{dialect.QuoteForColumnName("Id")}, " +
            $"{dialect.QuoteForColumnName("Type")}, " +
            $"{dialect.QuoteForColumnName("Content")}, " +
            $"{dialect.QuoteForColumnName("Version")}) " +
            $"SELECT series, '{typeof(Interaction).FullName}', '{{}}', 1 " +
            $"FROM generate_series(1, {SeededInteractions}) AS series",
            cancellationToken);

        await ExecuteAsync(
            transaction,
            $"INSERT INTO {tableName} (" +
            $"{dialect.QuoteForColumnName("DocumentId")}, " +
            $"{dialect.QuoteForColumnName("ItemId")}, " +
            $"{dialect.QuoteForColumnName("Status")}, " +
            $"{dialect.QuoteForColumnName("AgentId")}, " +
            $"{dialect.QuoteForColumnName("CreatedUtc")}) " +
            "SELECT series, " +
            "'interaction-' || LPAD(series::text, 6, '0'), " +
            $"(series % {statusCount}), " +
            $"'agent-' || LPAD((series % {SeededAgents})::text, 4, '0'), " +
            "TIMESTAMP '2026-07-16 12:00:00' + (series * INTERVAL '1 second') " +
            $"FROM generate_series(1, {SeededInteractions}) AS series",
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
        var agentIds = Enumerable.Range(0, 5).Select(index => $"agent-{index:D4}").ToArray();
        var sql = InteractionQueries.BuildActiveCountByAgentSql(store.Configuration, agentIds.Length);

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

        for (var index = 0; index < agentIds.Length; index++)
        {
            AddParameter(command, InteractionQueries.AgentIdParameterName(index)).Value = agentIds[index];
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
