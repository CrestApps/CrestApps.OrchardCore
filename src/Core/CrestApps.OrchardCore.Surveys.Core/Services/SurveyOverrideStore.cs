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
}
