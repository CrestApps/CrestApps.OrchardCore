using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IOmnichannelChannelEndpointManager"/> that delegates storage
/// to <see cref="IOmnichannelChannelEndpointStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class OmnichannelChannelEndpointManager : CatalogManager<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointManager
{
    private readonly IOmnichannelChannelEndpointStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointManager"/> class.
    /// </summary>
    /// <param name="store">The underlying channel endpoint store.</param>
    /// <param name="handlers">The catalog entry handlers for channel endpoint entries.</param>
    /// <param name="logger">The logger instance.</param>
    public OmnichannelChannelEndpointManager(
        IOmnichannelChannelEndpointStore store,
        IEnumerable<ICatalogEntryHandler<OmnichannelChannelEndpoint>> handlers,
        ILogger<CatalogManager<OmnichannelChannelEndpoint>> logger)
    : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress)
    {
        return _store.GetByServiceAddressAsync(channel, serviceAddress);
    }
}
