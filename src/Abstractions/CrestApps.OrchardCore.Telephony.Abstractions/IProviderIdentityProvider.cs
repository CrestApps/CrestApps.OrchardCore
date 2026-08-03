using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Contributes canonical provider identities so that voice ingress can resolve provider aliases to a
/// single stable technical name without referencing provider implementation assemblies. Provider modules
/// implement this contract to register their canonical name and any alternate runtime names.
/// </summary>
public interface IProviderIdentityProvider
{
    /// <summary>
    /// Gets the canonical provider identities contributed by the implementing provider module.
    /// </summary>
    /// <returns>The canonical provider identities and their aliases.</returns>
    IEnumerable<ProviderIdentity> GetIdentities();
}
