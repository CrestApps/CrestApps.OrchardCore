using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="IProviderIdentityResolver"/> implementation. Canonical identities and
/// aliases are gathered from every registered <see cref="IProviderIdentityProvider"/> so provider modules
/// contribute their own identities without the Contact Center referencing provider implementation assemblies.
/// </summary>
public sealed class ProviderIdentityResolver : IProviderIdentityResolver
{
    private readonly Dictionary<string, string> _aliasToCanonical;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderIdentityResolver"/> class.
    /// </summary>
    /// <param name="identityProviders">The registered provider identity contributors.</param>
    public ProviderIdentityResolver(IEnumerable<IProviderIdentityProvider> identityProviders)
    {
        _aliasToCanonical = BuildMap(identityProviders);
    }

    /// <inheritdoc/>
    public string Canonicalize(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return providerName;
        }

        return _aliasToCanonical.TryGetValue(providerName, out var canonical)
            ? canonical
            : providerName;
    }

    private static Dictionary<string, string> BuildMap(IEnumerable<IProviderIdentityProvider> identityProviders)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (identityProviders is null)
        {
            return map;
        }

        foreach (var identityProvider in identityProviders)
        {
            foreach (var identity in identityProvider.GetIdentities())
            {
                if (identity is null || string.IsNullOrWhiteSpace(identity.CanonicalName))
                {
                    continue;
                }

                map[identity.CanonicalName] = identity.CanonicalName;

                foreach (var alias in identity.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        map[alias] = identity.CanonicalName;
                    }
                }
            }
        }

        return map;
    }
}
