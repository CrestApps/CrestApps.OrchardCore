using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The default implementation of <see cref="ISmsNumberRouteManager"/>.
/// </summary>
public sealed class SmsNumberRouteManager : CatalogManager<SmsNumberRoute>, ISmsNumberRouteManager
{
    private readonly ISmsNumberRouteStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNumberRouteManager"/> class.
    /// </summary>
    /// <param name="store">The underlying number-route store.</param>
    /// <param name="handlers">The catalog entry handlers for number routes.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsNumberRouteManager(
        ISmsNumberRouteStore store,
        IEnumerable<ICatalogEntryHandler<SmsNumberRoute>> handlers,
        ILogger<CatalogManager<SmsNumberRoute>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SmsNumberRoute> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        var route = await _store.FindByDialedNumberAsync(dialedNumber, cancellationToken);

        if (route is not null)
        {
            await LoadAsync(route, cancellationToken);
        }

        return route;
    }
}
