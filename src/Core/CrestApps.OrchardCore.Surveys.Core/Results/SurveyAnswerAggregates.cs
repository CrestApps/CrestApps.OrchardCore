using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Services;
using Dapper;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Computes results in SQL over the answer index and the response index, for completed, non-test responses. Long lists
/// of response ids run in chunks of <see cref="SurveyIndexSql.ChunkSize"/>.
/// </summary>
public sealed class SurveyAnswerAggregates : ISurveyAnswerAggregates
{
    private const int DistributionLimit = 200;

    private readonly SurveyIndexSql _sql;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnswerAggregates"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyAnswerAggregates(ISession session)
    {
        _sql = new SurveyIndexSql(session);
    }

    /// <inheritdoc />
    public async Task<long> CountResponsesAsync(SurveyAnswerFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        long total = 0;

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"COUNT(*) AS {_sql.Alias("Total")}");
            WhereResponses(builder, parameters, filter, chunk);

            var rows = await _sql.QueryAsync(builder, parameters, cancellationToken);
            total += SurveyIndexSql.Long(SurveyIndexSql.First(rows)?["Total"]);
        }

        return total;
    }

    /// <inheritdoc />
    public async Task<long> CountAnsweredAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        long total = 0;

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"COUNT(DISTINCT {_sql.Column(nameof(SurveyAnswerIndex.ResponseId))}) AS {_sql.Alias("Total")}");
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            WhereQuestion(builder, parameters, questionId);
            builder.WhereAnd($"({_sql.Column(nameof(SurveyAnswerIndex.ChoiceId))} IS NOT NULL OR {_sql.Column(nameof(SurveyAnswerIndex.NumberValue))} IS NOT NULL OR {_sql.Column(nameof(SurveyAnswerIndex.DateValue))} IS NOT NULL OR {_sql.Column(nameof(SurveyAnswerIndex.TextValue))} IS NOT NULL)");

            var rows = await _sql.QueryAsync(builder, parameters, cancellationToken);
            total += SurveyIndexSql.Long(SurveyIndexSql.First(rows)?["Total"]);
        }

        return total;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>> CountChoicesAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var counts = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        var row = _sql.Column(nameof(SurveyAnswerIndex.RowId));
        var choice = _sql.Column(nameof(SurveyAnswerIndex.ChoiceId));

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{row} AS {_sql.Alias("RowId")}, {choice} AS {_sql.Alias("ChoiceId")}, COUNT(*) AS {_sql.Alias("Total")}");
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            WhereQuestion(builder, parameters, questionId);
            builder.WhereAnd($"{choice} IS NOT NULL");
            builder.GroupBy($"{row}, {choice}");

            foreach (var item in await _sql.QueryAsync(builder, parameters, cancellationToken))
            {
                var rowId = SurveyIndexSql.Text(item["RowId"]) ?? string.Empty;
                var choiceId = SurveyIndexSql.Text(item["ChoiceId"]);

                if (!counts.TryGetValue(rowId, out var choices))
                {
                    counts[rowId] = choices = new Dictionary<string, long>(StringComparer.Ordinal);
                }

                choices[choiceId] = choices.GetValueOrDefault(choiceId) + SurveyIndexSql.Long(item["Total"]);
            }
        }

        return counts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, long>)pair.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<SurveyNumberStatistics> NumberStatisticsAsync(SurveyAnswerFilter filter, string questionId, string rowId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var number = _sql.Column(nameof(SurveyAnswerIndex.NumberValue));

        if (filter.ResponseIds is not null)
        {
            // Values of a segment are read and summarized here, since the segment runs in chunks.
            var values = new List<decimal>();

            foreach (var chunk in SurveyIndexSql.Chunks(filter))
            {
                var builder = NumberQuery(filter, questionId, rowId, chunk, $"{number} AS {_sql.Alias("Value")}", out var parameters);

                values.AddRange((await _sql.QueryAsync(builder, parameters, cancellationToken)).Select(item => SurveyIndexSql.Decimal(item["Value"]) ?? 0));
            }

            return SurveyNumberSummary.Summarize(values);
        }

        var totals = NumberQuery(filter, questionId, rowId, null, $"COUNT({number}) AS {_sql.Alias("Total")}, AVG({number}) AS {_sql.Alias("Average")}, MIN({number}) AS {_sql.Alias("Lowest")}, MAX({number}) AS {_sql.Alias("Highest")}", out var totalParameters);
        var summary = SurveyIndexSql.First(await _sql.QueryAsync(totals, totalParameters, cancellationToken));
        var statistics = new SurveyNumberStatistics
        {
            Count = SurveyIndexSql.Long(summary?["Total"]),
        };

        if (statistics.Count == 0)
        {
            return statistics;
        }

        statistics.Average = SurveyIndexSql.Decimal(summary["Average"]);
        statistics.Min = SurveyIndexSql.Decimal(summary["Lowest"]);
        statistics.Max = SurveyIndexSql.Decimal(summary["Highest"]);

        var distribution = NumberQuery(filter, questionId, rowId, null, $"{number} AS {_sql.Alias("Value")}, COUNT(*) AS {_sql.Alias("Total")}", out var distributionParameters);
        distribution.GroupBy(number);
        distribution.OrderBy(number);

        // Only the first values are kept, so only those are read: a free number question can have a value per answer.
        distribution.Take(SurveyIndexSql.Number(DistributionLimit));

        foreach (var item in await _sql.QueryAsync(distribution, distributionParameters, cancellationToken))
        {
            if (statistics.Distribution.Count >= DistributionLimit)
            {
                break;
            }

            statistics.Distribution[SurveyIndexSql.Decimal(item["Value"]) ?? 0] = SurveyIndexSql.Long(item["Total"]);
        }

        // The median is read in order from the middle, which every dialect can page.
        var middle = NumberQuery(filter, questionId, rowId, null, $"{number} AS {_sql.Alias("Value")}", out var middleParameters);
        middle.OrderBy(number);
        middle.Skip(SurveyIndexSql.Number((int)((statistics.Count - 1) / 2)));
        middle.Take(statistics.Count % 2 == 0 ? "2" : "1");

        var center = (await _sql.QueryAsync(middle, middleParameters, cancellationToken)).Select(item => SurveyIndexSql.Decimal(item["Value"]) ?? 0).ToList();
        statistics.Median = center.Count == 0 ? null : center.Average();

        return statistics;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> TextSamplesAsync(SurveyAnswerFilter filter, string questionId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (take <= 0)
        {
            return [];
        }

        var samples = new List<(DateTime Completed, string Text)>();
        var text = _sql.Column(nameof(SurveyAnswerIndex.TextValue));
        var completed = _sql.Column(nameof(SurveyAnswerIndex.CompletedUtc));

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{text} AS {_sql.Alias("TextValue")}, {completed} AS {_sql.Alias("CompletedUtc")}");
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            WhereQuestion(builder, parameters, questionId);
            builder.WhereAnd($"{text} IS NOT NULL");
            builder.OrderByDescending(completed);
            builder.Take(SurveyIndexSql.Number(take));

            samples.AddRange((await _sql.QueryAsync(builder, parameters, cancellationToken))
                .Select(item => (SurveyIndexSql.Date(item["CompletedUtc"]) ?? DateTime.MinValue, SurveyIndexSql.Text(item["TextValue"]))));
        }

        return samples
            .Where(sample => !string.IsNullOrWhiteSpace(sample.Text))
            .OrderByDescending(sample => sample.Completed)
            .Take(take)
            .Select(sample => sample.Text)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ResponsesWithChoiceAsync(SurveyAnswerFilter filter, string questionId, string choiceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var response = _sql.Column(nameof(SurveyAnswerIndex.ResponseId));

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{response} AS {_sql.Alias("ResponseId")}");
            builder.Distinct();
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            WhereQuestion(builder, parameters, questionId);
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.ChoiceId))} = @ChoiceId");
            parameters.Add("ChoiceId", choiceId);

            ids.UnionWith((await _sql.QueryAsync(builder, parameters, cancellationToken)).Select(item => SurveyIndexSql.Text(item["ResponseId"])).Where(id => id is not null));
        }

        return ids;
    }

    private ISqlBuilder NumberQuery(SurveyAnswerFilter filter, string questionId, string rowId, IReadOnlyCollection<string> chunk, string selector, out DynamicParameters parameters)
    {
        var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
        parameters = new DynamicParameters();
        builder.Selector(selector);
        _sql.WhereAnswers(builder, parameters, filter, chunk);
        WhereQuestion(builder, parameters, questionId);
        builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.NumberValue))} IS NOT NULL");

        if (string.IsNullOrEmpty(rowId))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.RowId))} IS NULL");
        }
        else
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.RowId))} = @RowId");
            parameters.Add("RowId", rowId);
        }

        return builder;
    }

    private void WhereQuestion(ISqlBuilder builder, DynamicParameters parameters, string questionId)
    {
        builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.QuestionId))} = @QuestionId");
        parameters.Add("QuestionId", questionId);
    }

    private void WhereResponses(ISqlBuilder builder, DynamicParameters parameters, SurveyAnswerFilter filter, IReadOnlyCollection<string> chunk)
    {
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.SurveyId))} = @SurveyId");
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} = @Status");
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
        parameters.Add("SurveyId", filter.SurveyId);
        parameters.Add("Status", nameof(Models.SurveyResponseStatus.Completed));
        parameters.Add("IsTest", false);

        if (filter.VersionNumbers is { Count: > 0 })
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.VersionNumber))} IN @VersionNumbers");
            parameters.Add("VersionNumbers", filter.VersionNumbers.ToArray());
        }

        if (filter.CompletedFromUtc is { } from)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} >= @FromUtc");
            parameters.Add("FromUtc", from);
        }

        if (filter.CompletedToUtc is { } to)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} < @ToUtc");
            parameters.Add("ToUtc", to);
        }

        if (!string.IsNullOrEmpty(filter.CollectorId))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CollectorId))} = @CollectorId");
            parameters.Add("CollectorId", filter.CollectorId);
        }

        if (!string.IsNullOrEmpty(filter.ContextKind))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.ContextKind))} = @ContextKind");
            parameters.Add("ContextKind", filter.ContextKind);
        }

        if (!string.IsNullOrEmpty(filter.ContextRef))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.ContextRef))} = @ContextRef");
            parameters.Add("ContextRef", filter.ContextRef);
        }

        if (chunk is not null)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.ItemId))} IN @ResponseIds");
            parameters.Add("ResponseIds", chunk.ToArray());
        }
    }
}
