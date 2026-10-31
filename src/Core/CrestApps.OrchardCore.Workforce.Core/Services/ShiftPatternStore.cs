using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores rotations.
/// </summary>
internal sealed class ShiftPatternStore : DocumentCatalog<ShiftPattern, ShiftPatternIndex>
{
    public ShiftPatternStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<ShiftPattern>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var patterns = await Session.Query<ShiftPattern, ShiftPatternIndex>(index => index.TeamId == teamId && index.Enabled, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return patterns.ToArray();
    }
}
