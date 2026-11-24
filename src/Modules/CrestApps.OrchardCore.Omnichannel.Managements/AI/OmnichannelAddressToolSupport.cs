using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Describes an omnichannel address for the address tools from an explicit list of fields. The settings other features
/// store on an address (outbound-line agents, messaging line, per-number SMS provider, provider number details) are
/// named only, never returned, because some of them belong to modules this one cannot read and none is needed to pick
/// an address.
/// </summary>
internal static class OmnichannelAddressToolSupport
{
    /// <summary>
    /// Describes one address.
    /// </summary>
    public static async Task<object> DescribeAsync(IServiceProvider services, OmnichannelChannelEndpoint endpoint, bool full)
    {
        var localClock = services.GetService<ILocalClock>();

        return new
        {
            id = endpoint.ItemId,
            displayText = endpoint.DisplayText,
            addressType = endpoint.GetAddressType(),
            value = endpoint.Value,
            capabilities = endpoint.GetCapabilities(),
            providerName = endpoint.ProviderName,
            description = full ? endpoint.Description : null,
            featureSettings = full ? endpoint.Properties?.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray() ?? [] : null,
            created = full ? await AIModuleTools.ToLocalAsync(localClock, endpoint.CreatedUtc) : null,
            modified = full ? await AIModuleTools.ToLocalAsync(localClock, endpoint.ModifiedUtc) : null,
            editUrl = OmnichannelToolSupport.GetPath(services, "ChannelEndpoints", "Edit", endpoint.ItemId),
        };
    }
}
