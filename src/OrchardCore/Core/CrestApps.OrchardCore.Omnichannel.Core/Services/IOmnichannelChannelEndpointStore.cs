using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public interface IOmnichannelChannelEndpointStore : ICatalog<OmnichannelChannelEndpoint>
{
    ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress);
}
