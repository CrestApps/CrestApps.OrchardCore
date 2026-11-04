using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores time clock kiosks.
/// </summary>
internal sealed class TimeClockKioskStore : DocumentCatalog<TimeClockKiosk, TimeClockKioskIndex>
{
    public TimeClockKioskStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<TimeClockKiosk>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }

    public Task<TimeClockKiosk> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return Session.Query<TimeClockKiosk, TimeClockKioskIndex>(index => index.TokenHash == tokenHash, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }
}
