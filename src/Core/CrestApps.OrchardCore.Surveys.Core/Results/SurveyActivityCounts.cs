using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using Dapper;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Counts the activity of every survey at once, grouped by survey in SQL: the completed, non-test responses in a range
/// and the non-test responses waiting for a grader. Two statements whatever the number of surveys, so a dashboard on a
/// site with thousands of surveys (every Learning quiz owns one) never runs queries per survey; only the surveys with
/// activity come back.
/// </summary>
public sealed class SurveyActivityCounts
{
    private readonly SurveyIndexSql _sql;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyActivityCounts"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyActivityCounts(ISession session)
    {
        _sql = new SurveyIndexSql(session);
    }

    /// <summary>
    /// Counts each survey's completed responses in a range and its responses waiting for a grader.
    /// </summary>
    /// <param name="fromUtc">The start of the range, or <see langword="null"/>.</param>
    /// <param name="toUtc">The end of the range (excluded), or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The counts of the surveys with any, by survey id.</returns>
    public async Task<IReadOnlyDictionary<string, (long Completed, long Pending)>> CountBySurveyAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var surveyId = _sql.Column(nameof(SurveyResponseIndex.SurveyId));
        var counts = new Dictionary<string, (long Completed, long Pending)>(StringComparer.Ordinal);

        var completed = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
        var completedParameters = new DynamicParameters();
        completed.Selector($"{surveyId} AS SurveyId, COUNT(*) AS Total");
        completed.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Status))} = @Status");
        completed.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
        completedParameters.Add("Status", nameof(SurveyResponseStatus.Completed));
        completedParameters.Add("IsTest", false);

        if (fromUtc is { } from)
        {
            completed.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} >= @FromUtc");
            completedParameters.Add("FromUtc", from);
        }

        if (toUtc is { } to)
        {
            completed.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.CompletedUtc))} < @ToUtc");
            completedParameters.Add("ToUtc", to);
        }

        completed.GroupBy(surveyId);

        foreach (var row in await _sql.QueryAsync(completed, completedParameters, cancellationToken))
        {
            var id = SurveyIndexSql.Text(row["SurveyId"]);

            if (!string.IsNullOrEmpty(id))
            {
                counts[id] = (SurveyIndexSql.Long(row["Total"]), 0);
            }
        }

        var pending = _sql.Builder<SurveyResponseIndex>(SurveysConstants.ResponsesCollectionName);
        var pendingParameters = new DynamicParameters();
        pending.Selector($"{surveyId} AS SurveyId, COUNT(*) AS Total");
        pending.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.Grading))} = @Grading");
        pending.WhereAnd($"{_sql.Column(nameof(SurveyResponseIndex.IsTest))} = @IsTest");
        pendingParameters.Add("Grading", nameof(GradingStatus.Pending));
        pendingParameters.Add("IsTest", false);
        pending.GroupBy(surveyId);

        foreach (var row in await _sql.QueryAsync(pending, pendingParameters, cancellationToken))
        {
            var id = SurveyIndexSql.Text(row["SurveyId"]);

            if (!string.IsNullOrEmpty(id))
            {
                var current = counts.GetValueOrDefault(id);
                counts[id] = (current.Completed, SurveyIndexSql.Long(row["Total"]));
            }
        }

        return counts;
    }
}
