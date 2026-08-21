using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

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
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsBroadcast>> GetByStatusAsync(SmsBroadcastStatus status, CancellationToken cancellationToken = default)
    {
        var value = status.ToString();

        var broadcasts = await Session.Query<SmsBroadcast, SmsBroadcastIndex>(
                index => index.Status == value,
                collection: TelephonySmsStorage.CollectionName)
            .ListAsync(cancellationToken);

        return broadcasts.ToArray();
    }
}
