using CrestApps.OrchardCore.Surveys.Core.Indexes;
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
    public async IAsyncEnumerable<SurveyResponse> ReadAsync(SurveyResultsScope scope, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        for (var skip = 0; ; skip += SurveysConstants.PageSize)
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

            if (scope.VersionNumbers is { Count: > 0 })
            {
                var numbers = scope.VersionNumbers.ToArray();
                query = query.Where(index => index.VersionNumber.IsIn(numbers));
            }

            if (scope.FromUtc is { } from)
            {
                query = query.Where(index => index.StartedUtc >= from);
            }

            if (scope.ToUtc is { } to)
            {
                query = query.Where(index => index.StartedUtc < to);
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

            var page = (await query.OrderBy(index => index.Id).Skip(skip).Take(SurveysConstants.PageSize).ListAsync(cancellationToken)).ToList();

            foreach (var response in page)
            {
                yield return response;
            }

            if (page.Count < SurveysConstants.PageSize)
            {
                yield break;
            }
        }
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

        if (scope.VersionNumbers is { Count: > 0 })
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.VersionNumber))} IN @VersionNumbers");
            parameters.Add("VersionNumbers", scope.VersionNumbers.ToArray());
        }

        if (scope.FromUtc is { } from)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.StartedUtc))} >= @FromUtc");
            parameters.Add("FromUtc", from);
        }

        if (scope.ToUtc is { } to)
        {
            builder.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.StartedUtc))} < @ToUtc");
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
