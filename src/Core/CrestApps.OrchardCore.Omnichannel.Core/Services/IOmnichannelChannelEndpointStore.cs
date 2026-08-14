using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the contract for omnichannel channel endpoint store.
/// </summary>
public interface IOmnichannelChannelEndpointStore : ICatalog<OmnichannelChannelEndpoint>
{
    /// <summary>
    /// Gets the channel endpoint matching the specified channel and service address.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="serviceAddress">The service address to match.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress, CancellationToken cancellationToken = default);
}
