using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores responses in their own collection, with optimistic concurrency so two tabs never overwrite each other.
/// </summary>
public sealed class SurveyResponseStore : DocumentCatalog<SurveyResponse, SurveyResponseIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyResponseStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.ResponsesCollectionName;
    }

    /// <inheritdoc />
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Gets the YesSql session, for queries of the core services.
    /// </summary>
    internal ISession YesSqlSession => Session;
}
