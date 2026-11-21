using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores collectors.
/// </summary>
public sealed class SurveyCollectorStore : DocumentCatalog<SurveyCollector, SurveyCollectorIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCollectorStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyCollectorStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Finds a collector by its public token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collector, or <see langword="null"/>.</returns>
    public async Task<SurveyCollector> FindByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        return await Session.Query<SurveyCollector, SurveyCollectorIndex>(index => index.Token == token, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the collectors of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collectors.</returns>
    public async Task<IReadOnlyList<SurveyCollector>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        var items = await Session.Query<SurveyCollector, SurveyCollectorIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Finds the default link of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collector, or <see langword="null"/>.</returns>
    public async Task<SurveyCollector> FindDefaultAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        return await Session.Query<SurveyCollector, SurveyCollectorIndex>(index => index.SurveyId == surveyId && index.IsDefault, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
