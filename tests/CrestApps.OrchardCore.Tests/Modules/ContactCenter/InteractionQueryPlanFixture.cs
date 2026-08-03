using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Builds an interaction table that is large and varied enough for a query planner to make a meaningful choice.
/// A planner picks a sequential read when a table is small enough that reading it is genuinely cheaper, so a
/// plan budget seeded with a handful of rows passes no matter how badly the query is written.
/// </summary>
internal static class InteractionQueryPlanFixture
{
    /// <summary>
    /// The number of interactions seeded before a plan is measured.
    /// </summary>
    public const int SeededInteractions = 4000;

    /// <summary>
    /// The number of distinct agents the seeded interactions are spread across.
    /// </summary>
    public const int SeededAgents = 40;

    /// <summary>
    /// Returns the name of the interaction index table for the supplied store.
    /// </summary>
    /// <param name="configuration">The YesSql configuration naming the table and collection.</param>
    public static string TableName(IConfiguration configuration)
        => configuration.TableNameConvention.GetIndexTable(typeof(InteractionIndex), ContactCenterStorage.CollectionName);

    /// <summary>
    /// Runs the real interaction migrations, in order, against the supplied transaction. The plan is measured
    /// against the schema the product ships: a hand-written copy of the table would prove a plan for indexes
    /// nobody deploys.
    /// </summary>
    /// <param name="configuration">The YesSql configuration the schema builder writes through.</param>
    /// <param name="transaction">The open transaction the migrations run on.</param>
    public static async Task MigrateAsync(IConfiguration configuration, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transaction);

        var migration = new InteractionIndexMigrations
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom1Async();
        await migration.UpdateFrom2Async();
        await migration.UpdateFrom3Async();
        await migration.UpdateFrom4Async();
    }

    /// <summary>
    /// Inserts the seed rows the plan is measured against.
    /// </summary>
    /// <param name="configuration">The YesSql configuration naming the table and dialect.</param>
    /// <param name="transaction">The open transaction the rows are written on.</param>
    /// <param name="cancellationToken">The token used to cancel the seeding.</param>
    public static async Task SeedAsync(IConfiguration configuration, DbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transaction);

        var dialect = configuration.SqlDialect;
        var tableName = dialect.QuoteForTableName(TableName(configuration), configuration.Schema);
        var statuses = Enum.GetValues<InteractionStatus>();
        var createdUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

        var sql = $"INSERT INTO {tableName} (" +
            $"{dialect.QuoteForColumnName("DocumentId")}, " +
            $"{dialect.QuoteForColumnName("ItemId")}, " +
            $"{dialect.QuoteForColumnName("Status")}, " +
            $"{dialect.QuoteForColumnName("AgentId")}, " +
            $"{dialect.QuoteForColumnName("CreatedUtc")}) " +
            "VALUES (@DocumentId, @ItemId, @Status, @AgentId, @CreatedUtc)";

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        var documentId = AddParameter(command, "DocumentId");
        var itemId = AddParameter(command, "ItemId");
        var status = AddParameter(command, "Status");
        var agentId = AddParameter(command, "AgentId");
        var created = AddParameter(command, "CreatedUtc");

        for (var i = 0; i < SeededInteractions; i++)
        {
            documentId.Value = i + 1;
            itemId.Value = $"interaction-{i:D6}";
            status.Value = (int)statuses[i % statuses.Length];
            agentId.Value = $"agent-{i % SeededAgents:D4}";
            created.Value = createdUtc.AddSeconds(i);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Returns the agent identifiers a plan should be measured with.
    /// </summary>
    public static string[] SampleAgentIds()
        => [.. Enumerable.Range(0, 5).Select(index => $"agent-{index:D4}")];

    private static DbParameter AddParameter(DbCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        command.Parameters.Add(parameter);

        return parameter;
    }
}
