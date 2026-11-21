using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores survey versions. Versions are immutable: callers create them and never update them.
/// </summary>
public sealed class SurveyVersionStore : DocumentCatalog<SurveyVersion, SurveyVersionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyVersionStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Lists the versions of a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions.</returns>
    public async Task<IReadOnlyList<SurveyVersion>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        var items = await Session.Query<SurveyVersion, SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Finds a version of a survey by number.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="number">The number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    public async Task<SurveyVersion> FindByNumberAsync(string surveyId, int number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        return await Session.Query<SurveyVersion, SurveyVersionIndex>(index => index.SurveyId == surveyId && index.Number == number, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
