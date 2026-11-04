using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores coverage requirements.
/// </summary>
internal sealed class CoverageRequirementStore : DocumentCatalog<CoverageRequirement, CoverageRequirementIndex>
{
    public CoverageRequirementStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<CoverageRequirement>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var requirements = await Session.Query<CoverageRequirement, CoverageRequirementIndex>(index => index.TeamId == teamId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return requirements.ToArray();
    }
}
