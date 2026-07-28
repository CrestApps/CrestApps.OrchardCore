using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the hand-written statements the interaction hot paths execute. The text lives here rather than inside
/// the store so the query-plan gate can execute the statement production executes: a gate that rebuilt an
/// equivalent statement of its own would prove a plan for a query nothing runs.
/// </summary>
public static class InteractionQueries
{
    /// <summary>
    /// Returns the parameter name the agent identifier at the supplied position is bound to.
    /// </summary>
    /// <param name="index">The position of the agent identifier within the batch.</param>
    public static string AgentIdParameterName(int index) => $"AgentId{index}";

    /// <summary>
    /// Builds the statement that counts, per agent, how much live work that agent is already holding. Routing
    /// runs it before every offer, so it is the statement the query-plan budget is asserted against.
    /// </summary>
    /// <param name="configuration">The YesSql configuration that names the table, schema, prefix and dialect.</param>
    /// <param name="agentIdCount">The number of agent identifiers the statement will be executed with.</param>
    public static string BuildActiveCountByAgentSql(IConfiguration configuration, int agentIdCount)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentOutOfRangeException.ThrowIfLessThan(agentIdCount, 1);

        var dialect = configuration.SqlDialect;
        var tableName = configuration.TableNameConvention.GetIndexTable(typeof(InteractionIndex), ContactCenterConstants.CollectionName);
        var agentColumn = dialect.QuoteForColumnName(nameof(InteractionIndex.AgentId));
        var statusColumn = dialect.QuoteForColumnName(nameof(InteractionIndex.Status));

        // One placeholder per agent rather than one bound collection. A data access layer that expands a bound
        // collection into placeholders does so only on providers that cannot bind an array; on the providers
        // that can, the collection is sent as a single value, and "IN" against a single value is not valid
        // syntax at all. Writing the placeholders here gives the statement one shape on every supported engine,
        // and it is that shape the query-plan gates measure.
        var agentPlaceholders = string.Join(
            ", ",
            Enumerable.Range(0, agentIdCount).Select(index => $"@{AgentIdParameterName(index)}"));

        // An inclusive IN can be answered from an index that leads with the status column. The chain of
        // inequalities this replaced could not be, so the planner had to read every interaction for the agent.
        var occupyingStatuses = string.Join(", ", InteractionStatuses.OccupyingAgent.Select(status => (int)status));

        var builder = new SqlBuilder(configuration.TablePrefix, dialect);
        builder.Select();
        builder.Selector($"{agentColumn}, COUNT(*) AS {dialect.QuoteForColumnName("ActiveCount")}");
        builder.Table(tableName, alias: null, configuration.Schema);
        builder.WhereAnd($"{agentColumn} IN ({agentPlaceholders})");
        builder.WhereAnd($"{statusColumn} IN ({occupyingStatuses})");
        builder.GroupBy(agentColumn);

        return builder.ToSqlString();
    }
}
