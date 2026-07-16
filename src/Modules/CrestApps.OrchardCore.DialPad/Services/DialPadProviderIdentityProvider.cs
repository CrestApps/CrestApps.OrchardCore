using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Contributes the canonical DialPad provider identity so DialPad-sourced deliveries, events, and calls
/// resolve to a single stable technical name before the Contact Center builds identity keys.
/// </summary>
internal sealed class DialPadProviderIdentityProvider : IProviderIdentityProvider
{
    /// <inheritdoc/>
    public IEnumerable<ProviderIdentity> GetIdentities()
    {
        yield return new ProviderIdentity(DialPadConstants.ProviderTechnicalName);
    }
}
