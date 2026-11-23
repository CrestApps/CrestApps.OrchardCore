using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores accommodations.
/// </summary>
public sealed class SurveyOverrideStore : DocumentCatalog<SurveyOverride, SurveyOverrideIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverrideStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyOverrideStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Lists the accommodations of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accommodations.</returns>
    public async Task<IReadOnlyList<SurveyOverride>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        var items = await Session.Query<SurveyOverride, SurveyOverrideIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Lists the accommodations of a survey that may apply to a user: those granted to the user, and those granted to an
    /// audience (whose membership the caller checks). The runner asks on every request, so other people's accommodations
    /// are never read.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accommodations.</returns>
    public async Task<IReadOnlyList<SurveyOverride>> ListForUserAsync(string surveyId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var items = await Session.Query<SurveyOverride, SurveyOverrideIndex>(
                index => index.SurveyId == surveyId && (index.UserId == userId || (index.UserId == null && index.AudienceId != null)),
                collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Reads a page of the accommodations of a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<SurveyOverride> Items)> PageBySurveyAsync(string surveyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return (0, []);
        }

        var query = Session.Query<SurveyOverride, SurveyOverrideIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, SurveysConstants.PageSize))
            .ListAsync(cancellationToken);

        return (count, items.ToList());
    }
}
