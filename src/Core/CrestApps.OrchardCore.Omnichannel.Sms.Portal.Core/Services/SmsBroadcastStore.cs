using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="ISmsBroadcastStore"/>.
/// </summary>
public sealed class SmsBroadcastStore : DocumentCatalog<SmsBroadcast, SmsBroadcastIndex>, ISmsBroadcastStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SmsBroadcastStore(ISession session)
        : base(session)
    {
        CollectionName = SmsPortalStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsBroadcast>> GetByStatusAsync(SmsBroadcastStatus status, CancellationToken cancellationToken = default)
    {
        var value = status.ToString();

        var broadcasts = await Session.Query<SmsBroadcast, SmsBroadcastIndex>(
                index => index.Status == value,
                collection: SmsPortalStorage.CollectionName)
            .ListAsync(cancellationToken);

        return broadcasts.ToArray();
    }
}
