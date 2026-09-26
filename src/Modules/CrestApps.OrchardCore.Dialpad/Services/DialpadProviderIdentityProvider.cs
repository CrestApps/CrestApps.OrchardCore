using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Contributes the canonical Dialpad provider identity so Dialpad-sourced deliveries, events, and calls
/// resolve to a single stable technical name before the Contact Center builds identity keys.
/// </summary>
internal sealed class DialpadProviderIdentityProvider : IProviderIdentityProvider
{
    /// <inheritdoc/>
    public IEnumerable<ProviderIdentity> GetIdentities()
    {
        yield return new ProviderIdentity(DialpadConstants.ProviderTechnicalName);
    }
}
