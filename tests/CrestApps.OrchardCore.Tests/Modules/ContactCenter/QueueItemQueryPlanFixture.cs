using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Builds a queue item table that is large and varied enough for a query planner to make a meaningful choice.
/// A planner picks a sequential read when a table is small enough that reading it is genuinely cheaper, so a
/// plan budget seeded with a handful of rows passes no matter how badly the query is written.
/// </summary>
internal static class QueueItemQueryPlanFixture
{
    /// <summary>
    /// The number of queue items seeded before a plan is measured.
    /// </summary>
    public const int SeededQueueItems = 4000;

    /// <summary>
    /// The number of distinct queues the seeded items are spread across.
    /// </summary>
    public const int SeededQueues = 40;

    /// <summary>
    /// Returns the name of the queue item index table for the supplied store.
    /// </summary>
    /// <param name="configuration">The YesSql configuration naming the table and collection.</param>
    public static string TableName(IConfiguration configuration)
        => configuration.TableNameConvention.GetIndexTable(typeof(QueueItemIndex), ContactCenterConstants.CollectionName);

    /// <summary>
    /// Runs the real queue item migrations, in order, against the supplied transaction. The plan is measured
    /// against the schema the product ships: a hand-written copy of the table would prove a plan for indexes
    /// nobody deploys.
    /// </summary>
    /// <param name="store">The YesSql store the migrations read table naming from.</param>
    /// <param name="configuration">The YesSql configuration the schema builder writes through.</param>
    /// <param name="transaction">The open transaction the migrations run on.</param>
    public static async Task MigrateAsync(IStore store, IConfiguration configuration, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(transaction);

        var migration = new QueueItemIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom3Async();
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
        var statuses = Enum.GetValues<QueueItemStatus>();
        var enqueuedUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

        var sql = $"INSERT INTO {tableName} (" +
            $"{dialect.QuoteForColumnName("DocumentId")}, " +
            $"{dialect.QuoteForColumnName("ItemId")}, " +
            $"{dialect.QuoteForColumnName("QueueId")}, " +
            $"{dialect.QuoteForColumnName("ActivityItemId")}, " +
            $"{dialect.QuoteForColumnName("ActivityClaimKey")}, " +
            $"{dialect.QuoteForColumnName("Status")}, " +
            $"{dialect.QuoteForColumnName("Priority")}, " +
            $"{dialect.QuoteForColumnName("EnqueuedUtc")}) " +
            "VALUES (@DocumentId, @ItemId, @QueueId, @ActivityItemId, @ActivityClaimKey, @Status, @Priority, @EnqueuedUtc)";

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        var documentId = AddParameter(command, "DocumentId");
        var itemId = AddParameter(command, "ItemId");
        var queueId = AddParameter(command, "QueueId");
        var activityItemId = AddParameter(command, "ActivityItemId");
        var claimKey = AddParameter(command, "ActivityClaimKey");
        var status = AddParameter(command, "Status");
        var priority = AddParameter(command, "Priority");
        var enqueued = AddParameter(command, "EnqueuedUtc");

        for (var i = 0; i < SeededQueueItems; i++)
        {
            documentId.Value = i + 1;
            itemId.Value = $"queue-item-{i:D6}";
            queueId.Value = $"queue-{i % SeededQueues:D4}";
            activityItemId.Value = $"activity-{i:D6}";

            // The claim key carries a unique constraint, so every seeded row needs its own value.
            claimKey.Value = $"claim-{i:D6}";
            // The status must not be a function of the queue alone. Taking it straight from the row number
            // correlates it with the queue, because the queue is also a function of the row number, and every
            // row of a given queue then lands on the same status — which leaves the sampled queues holding no
            // waiting items at all and every count the gates compare equal to zero.
            status.Value = (int)statuses[((i % SeededQueues) + (i / SeededQueues)) % statuses.Length];
            priority.Value = (int)InteractionPriority.Normal;
            enqueued.Value = enqueuedUtc.AddSeconds(i);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Returns the queue identifiers a plan should be measured with.
    /// </summary>
    public static string[] SampleQueueIds()
        => [.. Enumerable.Range(0, 5).Select(index => $"queue-{index:D4}")];

    private static DbParameter AddParameter(DbCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        command.Parameters.Add(parameter);

        return parameter;
    }
}
