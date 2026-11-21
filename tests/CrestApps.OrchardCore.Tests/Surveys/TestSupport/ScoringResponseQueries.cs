using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Response queries over a YesSql session, enough for the scoring tests, which also records the last query.
/// </summary>
internal sealed class ScoringResponseQueries : ISurveyResponseQueries
{
    private readonly ISession _session;

    public ScoringResponseQueries(ISession session)
    {
        _session = session;
    }

    public SurveyResponseQuery LastQuery { get; private set; }

    public async Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default)
        => await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.ItemId == responseId, collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync(cancellationToken);

    public async Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default)
    {
        LastQuery = query;

        var items = (await _session.Query<SurveyResponse, SurveyResponseIndex>(collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken))
            .Where(item => query.SurveyIds.Count == 0 || query.SurveyIds.Contains(item.SurveyId))
            .Where(item => query.VersionNumber is null || item.VersionNumber == query.VersionNumber)
            .Where(item => query.Statuses.Count == 0 || query.Statuses.Contains(item.Status))
            .Where(item => query.Grading is null || item.Grading == query.Grading)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ToList();

        return (items.Count, items.Skip(query.Skip).Take(query.Take).ToList());
    }

    public Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
        => _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.ResponsesCollectionName).CountAsync(cancellationToken);
}
