using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Dapper;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Builds dialect-safe statements over the Surveys index tables and runs them on the session's transaction.
/// </summary>
internal sealed class SurveyIndexSql
{
    /// <summary>
    /// The most ids an IN list holds; longer lists run in chunks.
    /// </summary>
    public const int ChunkSize = SurveyIdChunks.Size;

    private readonly ISession _session;
    private readonly IConfiguration _configuration;
    private readonly ISqlDialect _dialect;

    public SurveyIndexSql(ISession session)
    {
        _session = session;
        _configuration = session.Store.Configuration;
        _dialect = _configuration.SqlDialect;
    }

    public string Column(string name)
        => _dialect.QuoteForColumnName(name);

    /// <summary>
    /// Quotes a column alias for the dialect. A bare alias can be a reserved word (<c>Percent</c> on SQL Server), and
    /// PostgreSQL folds a bare alias to lower case, so every alias a statement reads a row back by is quoted.
    /// </summary>
    /// <param name="name">The alias.</param>
    /// <returns>The quoted alias.</returns>
    public string Alias(string name)
        => _dialect.QuoteForAliasName(name);

    public ISqlBuilder Builder<TIndex>(string collection)
    {
        var builder = new SqlBuilder(_configuration.TablePrefix, _dialect);
        builder.Select();
        builder.Table(_configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection), null, _configuration.Schema);

        return builder;
    }

    public static string Number(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds the filter's conditions on the answer index to a statement.
    /// </summary>
    /// <param name="builder">The statement.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="responseIds">The chunk of response ids, or <see langword="null"/>.</param>
    public void WhereAnswers(ISqlBuilder builder, DynamicParameters parameters, SurveyAnswerFilter filter, IReadOnlyCollection<string> responseIds)
    {
        builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.SurveyId))} = @SurveyId");
        parameters.Add("SurveyId", filter.SurveyId);

        if (filter.VersionNumbers is { Count: > 0 })
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.VersionNumber))} IN @VersionNumbers");
            parameters.Add("VersionNumbers", filter.VersionNumbers.ToArray());
        }

        if (filter.CompletedFromUtc is { } from)
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.CompletedUtc))} >= @FromUtc");
            parameters.Add("FromUtc", from);
        }

        if (filter.CompletedToUtc is { } to)
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.CompletedUtc))} < @ToUtc");
            parameters.Add("ToUtc", to);
        }

        if (!string.IsNullOrEmpty(filter.CollectorId))
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.CollectorId))} = @CollectorId");
            parameters.Add("CollectorId", filter.CollectorId);
        }

        if (!string.IsNullOrEmpty(filter.ContextKind))
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.ContextKind))} = @ContextKind");
            parameters.Add("ContextKind", filter.ContextKind);
        }

        if (!string.IsNullOrEmpty(filter.ContextRef))
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.ContextRef))} = @ContextRef");
            parameters.Add("ContextRef", filter.ContextRef);
        }

        if (responseIds is not null)
        {
            builder.WhereAnd($"{Column(nameof(SurveyAnswerIndex.ResponseId))} IN @ResponseIds");
            parameters.Add("ResponseIds", responseIds.ToArray());
        }
    }

    /// <summary>
    /// Splits the filter's response ids into chunks: one <see langword="null"/> chunk when the filter has none, and no
    /// chunk when its list is empty (nothing can match).
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The chunks.</returns>
    public static IEnumerable<IReadOnlyCollection<string>> Chunks(SurveyAnswerFilter filter)
    {
        if (filter.ResponseIds is null)
        {
            yield return null;

            yield break;
        }

        foreach (var chunk in SurveyIdChunks.Of(filter.ResponseIds))
        {
            yield return chunk;
        }
    }

    public async Task<IReadOnlyList<IDictionary<string, object>>> QueryAsync(ISqlBuilder builder, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        // The statement runs on the session's own transaction, so it first has to see what this unit of work wrote.
        await _session.FlushAsync(cancellationToken);
        var transaction = await _session.BeginTransactionAsync(cancellationToken);
        var rows = await transaction.Connection.QueryAsync(new CommandDefinition(builder.ToSqlString(), parameters, transaction, cancellationToken: cancellationToken));

        // Read by name without regard to case as well: the aliases are quoted, but a dialect that still folds them (or a
        // statement written elsewhere) must not read every value back as missing.
        return rows.Select(row => (IDictionary<string, object>)new Dictionary<string, object>((IDictionary<string, object>)row, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public static IDictionary<string, object> First(IReadOnlyList<IDictionary<string, object>> rows)
        => rows is { Count: > 0 } ? rows[0] : null;

    public static long Long(object value)
        => value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);

    public static decimal? Decimal(object value)
        => value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    public static string Text(object value)
        => value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    public static DateTime? Date(object value)
        => value switch
        {
            null or DBNull => null,
            DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed) => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
            _ => null,
        };
}
