using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Contributes the canonical Telnyx provider identity so Telnyx-sourced deliveries, events, and calls
/// resolve to a single stable technical name before the Contact Center builds identity keys.
/// </summary>
internal sealed class TelnyxProviderIdentityProvider : IProviderIdentityProvider
{
    /// <inheritdoc/>
    public IEnumerable<ProviderIdentity> GetIdentities()
    {
        yield return new ProviderIdentity(TelnyxConstants.ProviderTechnicalName);
    }
}
