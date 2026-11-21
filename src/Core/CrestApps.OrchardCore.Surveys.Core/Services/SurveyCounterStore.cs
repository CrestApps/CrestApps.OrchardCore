using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores survey counters, one document per survey and kind.
/// </summary>
public sealed class SurveyCounterStore : DocumentCatalog<SurveyCounter, SurveyCounterIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCounterStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyCounterStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Finds the counter of a survey, or <see langword="null"/>.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The counter, or <see langword="null"/>.</returns>
    public async Task<SurveyCounter> FindAsync(string surveyId, string kind, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(kind))
        {
            return null;
        }

        return await Session.Query<SurveyCounter, SurveyCounterIndex>(index => index.SurveyId == surveyId && index.Kind == kind, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Stops tracking a counter, so the next read loads it again from the database.
    /// </summary>
    /// <param name="counter">The counter.</param>
    public void Forget(SurveyCounter counter)
    {
        if (counter is not null)
        {
            Session.Detach(counter, SurveysConstants.CollectionName);
        }
    }

    /// <summary>
    /// Saves the pending changes of the session while the caller holds the survey's lock.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);
}
