using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using Dapper;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the statement that finds, for a batch of aggregates, the most recent event of some types before an instant:
/// the state each agent was already in when a report's period opened.
/// </summary>
/// <remarks>
/// <para>
/// The statement is one seek per aggregate, joined with <c>UNION ALL</c>, so a batch costs one round trip instead of one
/// per agent, and each seek stops at the first matching row walking the aggregate index backwards. A grouped
/// <c>MAX(OccurredUtc)</c> per aggregate would also be one statement, but no index ends in the event type and the time
/// under the aggregate, so with the event-type filter every engine reads each agent's whole history to find the
/// maximum: the cost would grow with the age of the log instead of with the number of agents.
/// </para>
/// <para>
/// Every part of the statement is portable: the per-aggregate limit is written by the dialect (<c>TOP</c> on SQL
/// Server, <c>LIMIT</c> elsewhere), each limited sub-select is a derived table so its <c>ORDER BY</c> is allowed on every
/// engine, and every value is its own named parameter rather than a list Dapper might pass as an array.
/// </para>
/// </remarks>
internal static class InteractionEventAnchorQuery
{
    /// <summary>
    /// The most aggregates one statement looks up. SQLite allows at most 500 terms in a compound select, and every
    /// engine stays far inside its parameter limit at this size.
    /// </summary>
    public const int MaxAggregatesPerStatement = 100;

    /// <summary>
    /// Builds the statement and its parameters for one batch of aggregates.
    /// </summary>
    /// <param name="configuration">The YesSql configuration that names the tables, schema, prefix and dialect.</param>
    /// <param name="aggregateType">The aggregate type the events were recorded against.</param>
    /// <param name="eventTypes">The event types to consider; every type when empty.</param>
    /// <param name="aggregateIds">The aggregates to look up, at most <see cref="MaxAggregatesPerStatement"/>.</param>
    /// <param name="beforeUtc">The exclusive upper bound on occurrence time.</param>
    /// <returns>The statement, which returns the document identifier of each aggregate's latest event, and its parameters.</returns>
    public static (string Sql, DynamicParameters Parameters) Build(
        IConfiguration configuration,
        string aggregateType,
        IReadOnlyList<string> eventTypes,
        IReadOnlyList<string> aggregateIds,
        DateTime beforeUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(aggregateType);
        ArgumentNullException.ThrowIfNull(eventTypes);
        ArgumentNullException.ThrowIfNull(aggregateIds);
        ArgumentOutOfRangeException.ThrowIfZero(aggregateIds.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(aggregateIds.Count, MaxAggregatesPerStatement);

        var dialect = configuration.SqlDialect;
        var table = configuration.TableNameConvention.GetIndexTable(typeof(InteractionEventIndex), ContactCenterStorage.CollectionName);

        string Column(string name) => dialect.QuoteForColumnName(name);

        var documentId = Column(nameof(InteractionEventIndex.DocumentId));
        var occurredUtc = Column(nameof(InteractionEventIndex.OccurredUtc));

        var parameters = new DynamicParameters();
        parameters.Add("AggregateType", aggregateType);
        parameters.Add("BeforeUtc", beforeUtc);

        var typeFilter = string.Empty;

        if (eventTypes.Count > 0)
        {
            var names = new List<string>(eventTypes.Count);

            for (var index = 0; index < eventTypes.Count; index++)
            {
                var name = string.Create(CultureInfo.InvariantCulture, $"Type{index}");
                parameters.Add(name, eventTypes[index]);
                names.Add("@" + name);
            }

            typeFilter = $"e.{Column(nameof(InteractionEventIndex.EventType))} IN ({string.Join(", ", names)})";
        }

        var statement = new StringBuilder();

        for (var index = 0; index < aggregateIds.Count; index++)
        {
            var idParameter = string.Create(CultureInfo.InvariantCulture, $"Id{index}");
            var alias = string.Create(CultureInfo.InvariantCulture, $"a{index}");
            parameters.Add(idParameter, aggregateIds[index]);

            var builder = new SqlBuilder(configuration.TablePrefix, dialect);
            builder.Select();
            builder.Selector($"e.{documentId}");
            builder.Table(table, "e", configuration.Schema);
            builder.WhereAnd($"e.{Column(nameof(InteractionEventIndex.AggregateType))} = @AggregateType");
            builder.WhereAnd($"e.{Column(nameof(InteractionEventIndex.AggregateId))} = @{idParameter}");
            builder.WhereAnd($"e.{occurredUtc} < @BeforeUtc");

            if (typeFilter.Length > 0)
            {
                builder.WhereAnd(typeFilter);
            }

            builder.OrderByDescending($"e.{occurredUtc}");
            builder.ThenOrderByDescending($"e.{documentId}");
            builder.Take("1");

            if (index > 0)
            {
                statement.Append(" UNION ALL ");
            }

            statement.Append("SELECT ");
            statement.Append(alias);
            statement.Append('.');
            statement.Append(documentId);
            statement.Append(" FROM (");
            statement.Append(builder.ToSqlString());
            statement.Append(") ");
            statement.Append(alias);
        }

        return (statement.ToString(), parameters);
    }
}
