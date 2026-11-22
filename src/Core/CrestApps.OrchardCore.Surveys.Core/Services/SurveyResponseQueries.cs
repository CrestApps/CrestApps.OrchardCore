using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads responses through the response index.
/// </summary>
public sealed class SurveyResponseQueries : ISurveyResponseQueries
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseQueries"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyResponseQueries(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(responseId))
        {
            return null;
        }

        return await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.ItemId == responseId, collection: SurveysConstants.ResponsesCollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = Math.Clamp(query.Take, 1, SurveysConstants.PageSize);
        var skip = Math.Max(0, query.Skip);
        var q = _session.Query<SurveyResponse, SurveyResponseIndex>(collection: SurveysConstants.ResponsesCollectionName);

        if (query.SurveyIds.Count > 0)
        {
            var ids = query.SurveyIds.ToArray();
            q = q.Where(index => index.SurveyId.IsIn(ids));
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            var userId = query.UserId;
            q = q.Where(index => index.UserId == userId);
        }

        if (!string.IsNullOrEmpty(query.ContextRef))
        {
            var contextRef = query.ContextRef;
            q = q.Where(index => index.ContextRef == contextRef);
        }

        if (query.ContextKinds.Count > 0)
        {
            var kinds = query.ContextKinds.ToArray();
            q = q.Where(index => index.ContextKind.IsIn(kinds));
        }

        if (query.ExcludedContextKinds.Count > 0)
        {
            var excluded = query.ExcludedContextKinds.ToArray();
            q = q.Where(index => index.ContextKind == null || index.ContextKind.IsNotIn(excluded));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.Select(status => status.ToString()).ToArray();
            q = q.Where(index => index.Status.IsIn(statuses));
        }

        if (query.Grading is { } grading)
        {
            var value = grading.ToString();
            q = q.Where(index => index.Grading == value);
        }

        if (query.VersionNumber is { } number)
        {
            q = q.Where(index => index.VersionNumber == number);
        }

        if (!string.IsNullOrEmpty(query.CollectorId))
        {
            var collectorId = query.CollectorId;
            q = q.Where(index => index.CollectorId == collectorId);
        }

        if (!query.IncludeTest)
        {
            q = q.Where(index => !index.IsTest);
        }

        if (query.StartedFromUtc is { } from)
        {
            q = q.Where(index => index.StartedUtc >= from);
        }

        if (query.StartedToUtc is { } to)
        {
            q = q.Where(index => index.StartedUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(index => index.Number.Contains(search));
        }

        var count = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(index => index.StartedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToList());
    }

    /// <inheritdoc />
    public async Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        return await _session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync(cancellationToken);
    }
}
