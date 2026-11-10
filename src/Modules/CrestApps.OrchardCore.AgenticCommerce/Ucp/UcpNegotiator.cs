using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Agrees a version and capabilities with a platform, as UCP defines: the platform's version must be one the store
/// serves, and a capability is kept when both offer it in the same version, and an extension only with its parent.
/// </summary>
public sealed class UcpNegotiator
{
    private readonly UcpProfileBuilder _profiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpNegotiator"/> class.
    /// </summary>
    /// <param name="profiles">The store's profile builder.</param>
    public UcpNegotiator(UcpProfileBuilder profiles)
    {
        _profiles = profiles;
    }

    /// <summary>
    /// Agrees with a platform whose profile is known, or with one that gave none: the newest version and everything
    /// the store offers.
    /// </summary>
    /// <param name="platformProfile">The platform's profile, or <see langword="null"/>.</param>
    /// <param name="requestedVersion">The version the <c>UCP-Agent</c> header asked for, when the platform gave no
    /// profile; or <see langword="null"/>.</param>
    public async Task<UcpNegotiation> NegotiateAsync(JsonObject platformProfile, string requestedVersion = null)
    {
        var version = UcpConstants.Versions[0];

        if (platformProfile is null && !string.IsNullOrEmpty(requestedVersion))
        {
            if (!UcpConstants.Versions.Contains(requestedVersion, StringComparer.Ordinal))
            {
                return new UcpNegotiation();
            }

            version = requestedVersion;
        }

        if (platformProfile?["ucp"]?["version"]?.GetValueKind() == JsonValueKind.String)
        {
            version = platformProfile["ucp"]["version"].GetValue<string>();

            if (!UcpConstants.Versions.Contains(version, StringComparer.Ordinal))
            {
                return new UcpNegotiation();
            }
        }

        var offer = await _profiles.GetOfferAsync(version);
        var platformCapabilities = platformProfile?["ucp"]?["capabilities"] as JsonObject;
        var negotiation = new UcpNegotiation { Version = version };

        foreach (var capability in offer.Capabilities)
        {
            if (platformCapabilities is not null && !Offers(platformCapabilities, capability.Name, capability.Version))
            {
                continue;
            }

            // AP2 locks a checkout to signed mandates, so it's only agreed with a platform whose profile says it signs
            // them; a platform that gave no profile could never complete.
            if (platformCapabilities is null && capability.Name is UcpConstants.Capabilities.Ap2Mandate or UcpConstants.Capabilities.Ap2MandateLegacy)
            {
                continue;
            }

            negotiation.Capabilities[capability.Name] = capability.Version;
            negotiation.Extends[capability.Name] = capability.Extends;
        }

        // An extension whose parent isn't agreed is dropped, until nothing changes.
        bool removed;

        do
        {
            removed = false;

            foreach (var name in negotiation.Capabilities.Keys.ToList())
            {
                var parents = negotiation.Extends[name];

                if (parents.Count > 0 && !parents.Any(negotiation.Capabilities.ContainsKey))
                {
                    negotiation.Capabilities.Remove(name);
                    negotiation.Extends.Remove(name);
                    removed = true;
                }
            }
        }
        while (removed);

        return negotiation;
    }

    private static bool Offers(JsonObject capabilities, string name, string version)
    {
        if (capabilities[name] is not JsonArray entries)
        {
            return false;
        }

        return entries.Any(entry => entry?["version"]?.GetValueKind() == JsonValueKind.String && entry["version"].GetValue<string>() == version);
    }
}
