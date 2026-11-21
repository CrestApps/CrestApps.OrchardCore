using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores surveys, one document each.
/// </summary>
public sealed class SurveyStore : DocumentCatalog<Survey, SurveyIndex>, INamedCatalog<Survey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public async ValueTask<Survey> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        return await Session.Query<Survey, SurveyIndex>(index => index.Name == trimmed, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the surveys a record owns.
    /// </summary>
    /// <param name="ownerRef">The owner as Kind:Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys.</returns>
    public async Task<IReadOnlyList<Survey>> ListByOwnerAsync(string ownerRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ownerRef))
        {
            return [];
        }

        var items = await Session.Query<Survey, SurveyIndex>(index => index.OwnerRef == ownerRef, collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
    }
}
