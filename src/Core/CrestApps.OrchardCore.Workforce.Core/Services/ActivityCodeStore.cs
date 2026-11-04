using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores activity codes.
/// </summary>
internal sealed class ActivityCodeStore : DocumentCatalog<ActivityCode, ActivityCodeIndex>
{
    public ActivityCodeStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<ActivityCode>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var codes = await Session.Query<ActivityCode, ActivityCodeIndex>(index => index.Enabled, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return codes.ToArray();
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Session.Query<ActivityCode, ActivityCodeIndex>(collection: CollectionName).CountAsync(cancellationToken);
    }
}
