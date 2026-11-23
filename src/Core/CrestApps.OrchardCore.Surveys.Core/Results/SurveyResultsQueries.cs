using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Dapper;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Reads responses for results: index facts without documents, documents a page at a time, and question scores.
/// </summary>
public sealed class SurveyResultsQueries
{
    private readonly ISession _session;
    private readonly SurveyIndexSql _sql;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResultsQueries"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyResultsQueries(ISession session)
    {
        _session = session;
        _sql = new SurveyIndexSql(session);
    }

    /// <summary>
    /// Lists the index facts of the responses in a scope, oldest first.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public async Task<IReadOnlyList<SurveyResponseRow>> ListRowsAsync(SurveyResultsScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var rows = new List<SurveyResponseRow>();
        var id = _sql.Column("Id");

        for (var afterId = 0L; ;)
        {
            var builder = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{id} AS RowNumber, {_sql.Column(nameof(SurveyResponseIndex.ItemId))} AS ResponseId, {_sql.Column(nameof(SurveyResponseIndex.Status))} AS Status, {_sql.Column(nameof(SurveyResponseIndex.UserId))} AS UserId, {_sql.Column(nameof(SurveyResponseIndex.VersionNumber))} AS VersionNumber, {_sql.Column(nameof(SurveyResponseIndex.StartedUtc))} AS StartedUtc, {_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} AS CompletedUtc, {_sql.Column(nameof(SurveyResponseIndex.Percent))} AS Percent, {_sql.Column(nameof(SurveyResponseIndex.Passed))} AS Passed");
            Where(builder, parameters, scope);
            builder.WhereAnd($"{id} > @AfterId");
            parameters.Add("AfterId", afterId);
            builder.OrderBy(id);
            builder.Take(SurveyIndexSql.Number(SurveysConstants.PageSize));

            var page = await _sql.QueryAsync(builder, parameters, cancellationToken);

            foreach (var item in page)
            {
                rows.Add(new SurveyResponseRow
                {
                    ResponseId = SurveyIndexSql.Text(item["ResponseId"]),
                    Status = SurveyIndexSql.Text(item["Status"]),
                    UserId = SurveyIndexSql.Text(item["UserId"]),
                    VersionNumber = (int)SurveyIndexSql.Long(item["VersionNumber"]),
                    StartedUtc = SurveyIndexSql.Date(item["StartedUtc"]) ?? DateTime.MinValue,
                    CompletedUtc = SurveyIndexSql.Date(item["CompletedUtc"]),
                    Percent = SurveyIndexSql.Decimal(item["Percent"]),
                    Passed = item["Passed"] is null or DBNull ? null : SurveyIndexSql.Long(item["Passed"]) != 0,
                });
            }

            if (page.Count < SurveysConstants.PageSize)
            {
                return rows;
            }

            afterId = SurveyIndexSql.Long(page[page.Count - 1]["RowNumber"]);
        }
    }

    /// <summary>
    /// Reads the response documents of a scope a page at a time, oldest first.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The responses.</returns>
    public IAsyncEnumerable<SurveyResponse> ReadAsync(SurveyResultsScope scope, CancellationToken cancellationToken = default)
        => ReadAsync(scope, 0, int.MaxValue, cancellationToken);

    /// <summary>
    /// Reads a slice of the response documents of a scope, oldest first, loading a page at a time.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="skip">How many responses to skip.</param>
    /// <param name="take">The most responses to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The responses.</returns>
    public async IAsyncEnumerable<SurveyResponse> ReadAsync(SurveyResultsScope scope, int skip, int take, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var read = 0;

        for (var offset = Math.Max(0, skip); read < take; offset += SurveysConstants.PageSize)
        {
            var size = (int)Math.Min(SurveysConstants.PageSize, (long)take - read);
            var filtered = Filter(scope);
            var ordered = scope.OrderById ? filtered.OrderBy(index => index.ItemId) : filtered.OrderBy(index => index.Id);
            var page = (await ordered.Skip(offset).Take(size).ListAsync(cancellationToken)).ToList();

            foreach (var response in page)
            {
                read++;

                yield return response;
            }

            if (page.Count < size)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Counts the responses of a scope with a count query, without loading a document.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public Task<int> CountAsync(SurveyResultsScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Filter(scope).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Counts the responses of a scope by status with one grouped query, without loading a document.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count of each status that has responses.</returns>
    public async Task<IReadOnlyDictionary<SurveyResponseStatus, long>> CountByStatusAsync(SurveyResultsScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var status = _sql.Column(nameof(SurveyResponseIndex.Status));
        var builder = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
        var parameters = new DynamicParameters();
        builder.Selector($"{status} AS Status, COUNT(*) AS Total");
        Where(builder, parameters, scope);
        builder.GroupBy(status);

        var counts = new Dictionary<SurveyResponseStatus, long>();

        foreach (var item in await _sql.QueryAsync(builder, parameters, cancellationToken))
        {
            if (Enum.TryParse<SurveyResponseStatus>(SurveyIndexSql.Text(item["Status"]), out var value))
            {
                counts[value] = counts.GetValueOrDefault(value) + SurveyIndexSql.Long(item["Total"]);
            }
        }

        return counts;
    }

    /// <summary>
    /// Lists the scores of the scored questions of the completed responses that match a filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scores.</returns>
    public async Task<IReadOnlyList<SurveyQuestionScore>> ListQuestionScoresAsync(SurveyAnswerFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var scores = new List<SurveyQuestionScore>();

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{_sql.Column(nameof(SurveyAnswerIndex.ResponseId))} AS ResponseId, {_sql.Column(nameof(SurveyAnswerIndex.QuestionId))} AS QuestionId, {_sql.Column(nameof(SurveyAnswerIndex.Score))} AS Score, {_sql.Column(nameof(SurveyAnswerIndex.MaxPoints))} AS MaxPoints");
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.Score))} IS NOT NULL");
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.MaxPoints))} IS NOT NULL");

            scores.AddRange((await _sql.QueryAsync(builder, parameters, cancellationToken)).Select(item => new SurveyQuestionScore(
                SurveyIndexSql.Text(item["ResponseId"]),
                SurveyIndexSql.Text(item["QuestionId"]),
                SurveyIndexSql.Decimal(item["Score"]) ?? 0,
                SurveyIndexSql.Decimal(item["MaxPoints"]) ?? 0)));
        }

        return scores;
    }

    /// <summary>
    /// Lists the number values of a question in the completed responses that match a filter, with when each response
    /// was completed.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completion time in UTC and the value of each answer.</returns>
    public async Task<IReadOnlyList<(DateTime CompletedUtc, decimal Value)>> ListNumberValuesAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var values = new List<(DateTime, decimal)>();
        var number = _sql.Column(nameof(SurveyAnswerIndex.NumberValue));

        foreach (var chunk in SurveyIndexSql.Chunks(filter))
        {
            var builder = _sql.Builder<SurveyAnswerIndex>(SurveysConstants.ResponsesCollectionName);
            var parameters = new DynamicParameters();
            builder.Selector($"{_sql.Column(nameof(SurveyAnswerIndex.CompletedUtc))} AS CompletedUtc, {number} AS Value");
            _sql.WhereAnswers(builder, parameters, filter, chunk);
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.QuestionId))} = @QuestionId");
            builder.WhereAnd($"{number} IS NOT NULL");
            builder.WhereAnd($"{_sql.Column(nameof(SurveyAnswerIndex.RowId))} IS NULL");
            parameters.Add("QuestionId", questionId);

            values.AddRange((await _sql.QueryAsync(builder, parameters, cancellationToken))
                .Where(item => SurveyIndexSql.Date(item["CompletedUtc"]) is not null)
                .Select(item => (SurveyIndexSql.Date(item["CompletedUtc"]).Value, SurveyIndexSql.Decimal(item["Value"]) ?? 0)));
        }

        return values;
    }

    /// <summary>
    /// Counts the non-test responses of a survey that wait for a grader.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public async Task<int> CountPendingGradingAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        var pending = nameof(GradingStatus.Pending);

        return await _session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Grading == pending && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the surveys that have completed, non-test responses waiting for a grader, without loading a document.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey ids.</returns>
    public async Task<IReadOnlyList<string>> ListSurveysPendingGradingAsync(CancellationToken cancellationToken = default)
    {
        var surveyId = _sql.Column(nameof(SurveyResponseIndex.SurveyId));
        var builder = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
        var parameters = new DynamicParameters();
        builder.Selector($"{surveyId} AS SurveyId");
        builder.Distinct();
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Grading))} = @Grading");
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} = @Status");
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
        parameters.Add("Grading", nameof(GradingStatus.Pending));
        parameters.Add("Status", nameof(SurveyResponseStatus.Completed));
        parameters.Add("IsTest", false);

        return (await _sql.QueryAsync(builder, parameters, cancellationToken))
            .Select(item => SurveyIndexSql.Text(item["SurveyId"]))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
    }

    /// <summary>
    /// Counts, per survey, the completed non-test responses completed in a period and the non-test responses that wait
    /// for a grader: two grouped statements per chunk of surveys, whatever the number of surveys.
    /// </summary>
    /// <param name="surveyIds">The surveys, or <see langword="null"/> for every survey.</param>
    /// <param name="fromUtc">The start of the period, or <see langword="null"/>.</param>
    /// <param name="toUtc">The end of the period (excluded), or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The counts of the surveys that have any, by survey id.</returns>
    public async Task<IReadOnlyDictionary<string, (long Completed, long Pending)>> CountActivityAsync(IEnumerable<string> surveyIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, (long Completed, long Pending)>(StringComparer.Ordinal);
        IEnumerable<string[]> chunks = surveyIds is null ? [null] : SurveyIdChunks.Of(surveyIds);
        var surveyId = _sql.Column(nameof(SurveyResponseIndex.SurveyId));

        foreach (var chunk in chunks)
        {
            foreach (var pending in new[] { false, true })
            {
                var builder = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
                var parameters = new DynamicParameters();
                builder.Selector($"{surveyId} AS SurveyId, COUNT(*) AS Total");
                builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
                parameters.Add("IsTest", false);

                if (pending)
                {
                    builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Grading))} = @Grading");
                    parameters.Add("Grading", nameof(GradingStatus.Pending));
                }
                else
                {
                    builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} = @Status");
                    parameters.Add("Status", nameof(SurveyResponseStatus.Completed));

                    if (fromUtc is { } from)
                    {
                        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} >= @FromUtc");
                        parameters.Add("FromUtc", from);
                    }

                    if (toUtc is { } to)
                    {
                        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} < @ToUtc");
                        parameters.Add("ToUtc", to);
                    }
                }

                if (chunk is not null)
                {
                    builder.WhereAnd($"{surveyId} IN @SurveyIds");
                    parameters.Add("SurveyIds", chunk);
                }

                builder.GroupBy(surveyId);

                foreach (var row in await _sql.QueryAsync(builder, parameters, cancellationToken))
                {
                    var id = SurveyIndexSql.Text(row["SurveyId"]);

                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var total = SurveyIndexSql.Long(row["Total"]);
                    var current = counts.GetValueOrDefault(id);
                    counts[id] = pending ? (current.Completed, current.Pending + total) : (current.Completed + total, current.Pending);
                }
            }
        }

        return counts;
    }

    private IQuery<SurveyResponse, SurveyResponseIndex> Filter(SurveyResultsScope scope)
    {
        var query = _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == scope.SurveyId, collection: SurveysConstants.ResponsesCollectionName);

        if (!scope.IncludeTest)
        {
            query = query.Where(index => !index.IsTest);
        }

        if (scope.CompletedOnly)
        {
            var completed = nameof(SurveyResponseStatus.Completed);
            query = query.Where(index => index.Status == completed);
        }

        if (scope.Statuses is { Count: > 0 })
        {
            var statuses = scope.Statuses.Select(status => status.ToString()).ToArray();
            query = query.Where(index => index.Status.IsIn(statuses));
        }

        if (scope.ExcludedStatuses is { Count: > 0 })
        {
            var excluded = scope.ExcludedStatuses.Select(status => status.ToString()).ToArray();
            query = query.Where(index => index.Status.IsNotIn(excluded) || index.IsTest);
        }

        if (scope.VersionNumbers is { Count: > 0 })
        {
            var numbers = scope.VersionNumbers.ToArray();
            query = query.Where(index => index.VersionNumber.IsIn(numbers));
        }

        if (scope.FromUtc is { } from)
        {
            query = query.Where(index => (index.CompletedUtc != null && index.CompletedUtc >= from) || (index.CompletedUtc == null && index.StartedUtc >= from));
        }

        if (scope.ToUtc is { } to)
        {
            query = query.Where(index => (index.CompletedUtc != null && index.CompletedUtc < to) || (index.CompletedUtc == null && index.StartedUtc < to));
        }

        if (!string.IsNullOrEmpty(scope.CollectorId))
        {
            var collector = scope.CollectorId;
            query = query.Where(index => index.CollectorId == collector);
        }

        if (!string.IsNullOrEmpty(scope.ContextKind))
        {
            var kind = scope.ContextKind;
            query = query.Where(index => index.ContextKind == kind);
        }

        return query;
    }

    private void Where(ISqlBuilder builder, DynamicParameters parameters, SurveyResultsScope scope)
    {
        builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.SurveyId))} = @SurveyId");
        parameters.Add("SurveyId", scope.SurveyId);

        if (!scope.IncludeTest)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
            parameters.Add("IsTest", false);
        }

        if (scope.CompletedOnly)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} = @Status");
            parameters.Add("Status", nameof(SurveyResponseStatus.Completed));
        }

        if (scope.Statuses is { Count: > 0 })
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} IN @Statuses");
            parameters.Add("Statuses", scope.Statuses.Select(status => status.ToString()).ToArray());
        }

        if (scope.ExcludedStatuses is { Count: > 0 })
        {
            builder.WhereAnd($"({_sql.Column(nameof(SurveyResponseIndex.Status))} NOT IN @ExcludedStatuses OR {_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @ExcludedTest)");
            parameters.Add("ExcludedStatuses", scope.ExcludedStatuses.Select(status => status.ToString()).ToArray());
            parameters.Add("ExcludedTest", true);
        }

        if (scope.VersionNumbers is { Count: > 0 })
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.VersionNumber))} IN @VersionNumbers");
            parameters.Add("VersionNumbers", scope.VersionNumbers.ToArray());
        }

        var completedUtc = _sql.Column(nameof(SurveyResponseIndex.CompletedUtc));
        var startedUtc = _sql.Column(nameof(SurveyResponseIndex.StartedUtc));

        if (scope.FromUtc is { } from)
        {
            builder.WhereAnd($"(({completedUtc} IS NOT NULL AND {completedUtc} >= @FromUtc) OR ({completedUtc} IS NULL AND {startedUtc} >= @FromUtc))");
            parameters.Add("FromUtc", from);
        }

        if (scope.ToUtc is { } to)
        {
            builder.WhereAnd($"(({completedUtc} IS NOT NULL AND {completedUtc} < @ToUtc) OR ({completedUtc} IS NULL AND {startedUtc} < @ToUtc))");
            parameters.Add("ToUtc", to);
        }

        if (!string.IsNullOrEmpty(scope.CollectorId))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CollectorId))} = @CollectorId");
            parameters.Add("CollectorId", scope.CollectorId);
        }

        if (!string.IsNullOrEmpty(scope.ContextKind))
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.ContextKind))} = @ContextKind");
            parameters.Add("ContextKind", scope.ContextKind);
        }
    }
}
