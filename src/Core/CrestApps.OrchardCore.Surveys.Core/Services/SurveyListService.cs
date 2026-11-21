using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads pages of the survey list with its filters.
/// </summary>
public sealed class SurveyListService
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyListService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyListService(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Reads a page of surveys, last changed first.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Survey> Items)> ListAsync(SurveyListQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = _session.Query<Survey, SurveyIndex>(collection: SurveysConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(index => index.Title.Contains(search) || index.Name.Contains(search));
        }

        if (!string.IsNullOrEmpty(query.Kind))
        {
            var kind = query.Kind;
            q = q.Where(index => index.Kind == kind);
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            var status = query.Status;
            q = q.Where(index => index.Status == status);
        }
        else
        {
            var archived = nameof(SurveyStatus.Archived);
            q = q.Where(index => index.Status != archived);
        }

        if (!query.IncludeOwned)
        {
            q = q.Where(index => index.OwnerRef == null || index.OwnerRef == string.Empty);
        }

        var items = (await q.ListAsync(cancellationToken))
            .OrderByDescending(survey => survey.ModifiedUtc ?? survey.CreatedUtc)
            .ToList();

        // Owner and tag filters are applied to the documents: owners are not indexed, and tags are free text.
        var filtered = items
            .Where(survey => string.IsNullOrEmpty(query.OwnerId) || survey.OwnerId == query.OwnerId)
            .Where(survey => string.IsNullOrEmpty(query.Tag) || survey.Tags.Contains(query.Tag, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return (filtered.Count, filtered.Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ToList());
    }

    /// <summary>
    /// Lists the tags in use, for the tag filter.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tags, sorted.</returns>
    public async Task<IReadOnlyList<string>> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _session.Query<Survey, SurveyIndex>(collection: SurveysConstants.CollectionName).ListAsync(cancellationToken);

        return items.SelectMany(survey => survey.Tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
