namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes the canonical technical identity of a telephony or voice provider together with the
/// alternate names (aliases) that resolve to it. A single provider family can register multiple
/// runtime names (for example a tenant-configured provider and a configuration-backed default
/// provider) that must all map to one stable identity before it is used to build inbox, event, or
/// call keys.
/// </summary>
public sealed class ProviderIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderIdentity"/> class.
    /// </summary>
    /// <param name="canonicalName">The stable canonical technical name of the provider.</param>
    /// <param name="aliases">The alternate provider names that resolve to <paramref name="canonicalName"/>.</param>
    public ProviderIdentity(string canonicalName, params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrEmpty(canonicalName);

        CanonicalName = canonicalName;
        Aliases = aliases is null || aliases.Length == 0
            ? []
            : aliases;
    }

    /// <summary>
    /// Gets the stable canonical technical name of the provider.
    /// </summary>
    public string CanonicalName { get; }

    /// <summary>
    /// Gets the alternate provider names that resolve to <see cref="CanonicalName"/>.
    /// </summary>
    public IReadOnlyCollection<string> Aliases { get; }
}
