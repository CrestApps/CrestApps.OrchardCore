using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public sealed class OmnichannelChannelEndpointManager : CatalogManager<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointManager
{
    private readonly IOmnichannelChannelEndpointStore _store;

    public OmnichannelChannelEndpointManager(
        IOmnichannelChannelEndpointStore store,
        IEnumerable<ICatalogEntryHandler<OmnichannelChannelEndpoint>> handlers,
        ILogger<CatalogManager<OmnichannelChannelEndpoint>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }
    public ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress)
    {
        return _store.GetByServiceAddressAsync(channel, serviceAddress);
    }
}
