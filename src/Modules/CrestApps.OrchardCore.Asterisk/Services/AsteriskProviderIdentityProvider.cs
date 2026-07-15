using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Contributes the canonical Asterisk provider identity so the tenant-configured provider and the
/// configuration-backed default provider resolve to a single stable technical name before the Contact
/// Center builds inbox, event, or call identity keys.
/// </summary>
internal sealed class AsteriskProviderIdentityProvider : IProviderIdentityProvider
{
    /// <inheritdoc/>
    public IEnumerable<ProviderIdentity> GetIdentities()
    {
        yield return new ProviderIdentity(
            AsteriskConstants.ProviderTechnicalName,
            AsteriskConstants.DefaultProviderTechnicalName);
    }
}
