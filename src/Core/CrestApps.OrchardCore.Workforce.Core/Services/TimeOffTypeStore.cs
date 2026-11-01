using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores time-off types.
/// </summary>
internal sealed class TimeOffTypeStore : DocumentCatalog<TimeOffType, TimeOffTypeIndex>
{
    public TimeOffTypeStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<TimeOffType>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }
}
