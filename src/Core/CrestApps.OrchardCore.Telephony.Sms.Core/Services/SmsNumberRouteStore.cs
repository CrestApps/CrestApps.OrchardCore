using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="ISmsNumberRouteStore"/>.
/// </summary>
public sealed class SmsNumberRouteStore : DocumentCatalog<SmsNumberRoute, SmsNumberRouteIndex>, ISmsNumberRouteStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNumberRouteStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SmsNumberRouteStore(ISession session)
        : base(session)
    {
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<SmsNumberRoute> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dialedNumber);

        return await Session.Query<SmsNumberRoute, SmsNumberRouteIndex>(
            index => index.DialedNumber == dialedNumber && index.Enabled,
            collection: TelephonySmsStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsNumberRoute>> GetByEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointId);

        var routes = await Session.Query<SmsNumberRoute, SmsNumberRouteIndex>(
            index => index.EndpointId == endpointId,
            collection: TelephonySmsStorage.CollectionName)
            .ListAsync(cancellationToken);

        return routes.ToArray();
    }
}
