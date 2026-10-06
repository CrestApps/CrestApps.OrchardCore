using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the hand-written statement predictive dialing measures a dialer profile's recent calls with. The text lives
/// here rather than inside the provider so a test can execute the statement production executes.
/// </summary>
/// <remarks>
/// <para>
/// The statement reads only what the platform already records: each call a profile places files a
/// <c>DialerAttemptStarted</c> event under the profile, which the aggregate index finds with one seek; the answer, the
/// agent's connection and the end of the call are found from there by the call's identifier, through the event index's
/// interaction key and the interaction index's item key. Nothing new is written on the call path to measure it, and the
/// history recorded before predictive dialing was enabled counts from the first cycle.
/// </para>
/// <para>
/// Every per-call value is a correlated sub-select rather than a join, so a call with more than one agent leg or no
/// interaction row still yields exactly one row, and the statement has one shape on every supported engine.
/// </para>
/// </remarks>
public static class DialerPacingQueries
{
    /// <summary>
    /// The name of the parameter the dialer profile identifier is bound to.
    /// </summary>
    public const string ProfileIdParameter = "ProfileId";

    /// <summary>
    /// The name of the parameter the start of the window is bound to.
    /// </summary>
    public const string FromUtcParameter = "FromUtc";

    /// <summary>
    /// The name of the parameter the aggregate type of dialer profile events is bound to.
    /// </summary>
    public const string AggregateTypeParameter = "AggregateType";

    /// <summary>
    /// The name of the parameter the attempt-started event type is bound to.
    /// </summary>
    public const string AttemptStartedParameter = "AttemptStarted";

    /// <summary>
    /// The name of the parameter the live-answered event type is bound to.
    /// </summary>
    public const string LiveAnsweredParameter = "LiveAnswered";

    /// <summary>
    /// The name of the parameter the agent-leg-answered event type is bound to.
    /// </summary>
    public const string AgentLegAnsweredParameter = "AgentLegAnswered";

    /// <summary>
    /// Builds the statement that returns one row per call the profile placed since the start of the window, newest
    /// first and at most <paramref name="maxRows"/> of them, with the moments the call was placed, answered by a person,
    /// connected to an agent, ended, and wrapped up.
    /// </summary>
    /// <param name="configuration">The YesSql configuration that names the tables, schema, prefix and dialect.</param>
    /// <param name="maxRows">The most rows to return.</param>
    /// <returns>The SQL statement.</returns>
    public static string BuildCallTimingsSql(IConfiguration configuration, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);

        var dialect = configuration.SqlDialect;
        var eventsTable = configuration.TableNameConvention.GetIndexTable(typeof(InteractionEventIndex), ContactCenterStorage.CollectionName);
        var interactionsTable = dialect.QuoteForTableName(
            configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(InteractionIndex), ContactCenterStorage.CollectionName),
            configuration.Schema);
        var qualifiedEventsTable = dialect.QuoteForTableName(configuration.TablePrefix + eventsTable, configuration.Schema);

        string Column(string name) => dialect.QuoteForColumnName(name);

        var interactionId = Column(nameof(InteractionEventIndex.InteractionId));
        var eventType = Column(nameof(InteractionEventIndex.EventType));
        var occurredUtc = Column(nameof(InteractionEventIndex.OccurredUtc));
        var itemId = Column(nameof(InteractionIndex.ItemId));

        string FirstEvent(string alias, string parameter)
            => $"(SELECT MIN({alias}.{occurredUtc}) FROM {qualifiedEventsTable} {alias} " +
               $"WHERE {alias}.{interactionId} = s.{interactionId} AND {alias}.{eventType} = @{parameter})";

        string InteractionValue(string alias, string column)
            => $"(SELECT {alias}.{Column(column)} FROM {interactionsTable} {alias} WHERE {alias}.{itemId} = s.{interactionId})";

        var selector = string.Join(
            ", ",
            $"s.{interactionId} AS {Column(nameof(DialerCallTimingRow.InteractionId))}",
            $"s.{occurredUtc} AS {Column(nameof(DialerCallTimingRow.StartedUtc))}",
            $"{FirstEvent("l", LiveAnsweredParameter)} AS {Column(nameof(DialerCallTimingRow.LiveAnsweredUtc))}",
            $"{FirstEvent("a", AgentLegAnsweredParameter)} AS {Column(nameof(DialerCallTimingRow.AgentJoinedUtc))}",
            $"{InteractionValue("e", nameof(InteractionIndex.EndedUtc))} AS {Column(nameof(DialerCallTimingRow.EndedUtc))}",
            $"{InteractionValue("w", nameof(InteractionIndex.WrapUpStartedUtc))} AS {Column(nameof(DialerCallTimingRow.WrapUpStartedUtc))}",
            $"{InteractionValue("c", nameof(InteractionIndex.WrapUpCompletedUtc))} AS {Column(nameof(DialerCallTimingRow.WrapUpCompletedUtc))}");

        var builder = new SqlBuilder(configuration.TablePrefix, dialect);
        builder.Select();
        builder.Selector(selector);
        builder.Table(eventsTable, "s", configuration.Schema);
        builder.WhereAnd($"s.{Column(nameof(InteractionEventIndex.AggregateType))} = @{AggregateTypeParameter}");
        builder.WhereAnd($"s.{Column(nameof(InteractionEventIndex.AggregateId))} = @{ProfileIdParameter}");
        builder.WhereAnd($"s.{occurredUtc} >= @{FromUtcParameter}");
        builder.WhereAnd($"s.{eventType} = @{AttemptStartedParameter}");
        builder.OrderByDescending($"s.{occurredUtc}");
        builder.Take(maxRows.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return builder.ToSqlString();
    }
}
