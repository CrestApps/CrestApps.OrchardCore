using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Applies the capabilities picked for an omnichannel address, for the address editor and the AI tool that share the rule.
/// </summary>
internal static class OmnichannelAddressCapabilities
{
    /// <summary>
    /// Merges the picked capabilities into the address's. The editor offers the capabilities of the enabled features;
    /// one whose feature is off is not offered, and is kept as it was rather than dropped, so switching a feature off
    /// and on again does not lose what the address does.
    /// </summary>
    /// <param name="addressOptions">The address types and capabilities the enabled features registered.</param>
    /// <param name="endpoint">The address.</param>
    /// <param name="picked">The capabilities picked; those not offered for the address type are ignored.</param>
    /// <returns>The address's capabilities.</returns>
    public static List<string> Merge(OmnichannelAddressOptions addressOptions, OmnichannelChannelEndpoint endpoint, IEnumerable<string> picked)
    {
        ArgumentNullException.ThrowIfNull(addressOptions);
        ArgumentNullException.ThrowIfNull(endpoint);

        var offered = addressOptions.GetCapabilities(endpoint.GetAddressType())
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ticked = (picked ?? [])
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .Select(capability => capability.Trim())
            .Where(offered.Contains)
            .Select(capability => addressOptions.Capabilities[capability].Name);

        var kept = endpoint.GetCapabilities().Where(capability => !offered.Contains(capability));

        return ticked.Concat(kept)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
