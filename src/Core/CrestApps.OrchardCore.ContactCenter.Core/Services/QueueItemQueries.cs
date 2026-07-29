using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the hand-written statements the queue hot paths execute. The text lives here rather than inside the
/// store so the query-plan gate can execute the statement production executes: a gate that rebuilt an
/// equivalent statement of its own would prove a plan for a query nothing runs.
/// </summary>
public static class QueueItemQueries
{
    /// <summary>
    /// Returns the parameter name the queue identifier at the supplied position is bound to.
    /// </summary>
    /// <param name="index">The position of the queue identifier within the batch.</param>
    public static string QueueIdParameterName(int index) => $"QueueId{index}";

    /// <summary>
    /// Builds the statement that counts, per queue, how many items are still waiting. The agent workspace runs
    /// it on every poll for every queue the agent belongs to, so it is the statement the query-plan budget is
    /// asserted against.
    /// </summary>
    /// <param name="configuration">The YesSql configuration that names the table, schema, prefix and dialect.</param>
    /// <param name="queueIdCount">The number of queue identifiers the statement will be executed with.</param>
    public static string BuildWaitingCountByQueueSql(IConfiguration configuration, int queueIdCount)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueIdCount, 1);

        var dialect = configuration.SqlDialect;
        var tableName = configuration.TableNameConvention.GetIndexTable(typeof(QueueItemIndex), ContactCenterConstants.CollectionName);
        var queueColumn = dialect.QuoteForColumnName(nameof(QueueItemIndex.QueueId));
        var statusColumn = dialect.QuoteForColumnName(nameof(QueueItemIndex.Status));

        // One placeholder per queue rather than one bound collection. A data access layer that expands a bound
        // collection into placeholders does so only on providers that cannot bind an array; on the providers
        // that can, the collection is sent as a single value, and "IN" against a single value is not valid
        // syntax at all. Writing the placeholders here gives the statement one shape on every supported engine,
        // and it is that shape the query-plan gates measure.
        var queuePlaceholders = string.Join(
            ", ",
            Enumerable.Range(0, queueIdCount).Select(index => $"@{QueueIdParameterName(index)}"));

        var builder = new SqlBuilder(configuration.TablePrefix, dialect);
        builder.Select();
        builder.Selector($"{queueColumn}, COUNT(*) AS {dialect.QuoteForColumnName("WaitingCount")}");
        builder.Table(tableName, alias: null, configuration.Schema);
        builder.WhereAnd($"{queueColumn} IN ({queuePlaceholders})");
        builder.WhereAnd($"{statusColumn} = {(int)QueueItemStatus.Waiting}");
        builder.GroupBy(queueColumn);

        return builder.ToSqlString();
    }
}
