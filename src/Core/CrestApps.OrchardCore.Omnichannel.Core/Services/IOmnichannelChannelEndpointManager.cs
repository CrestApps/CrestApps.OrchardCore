using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public interface IOmnichannelChannelEndpointManager : ICatalogManager<OmnichannelChannelEndpoint>
{
    ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress);
}
