using System.Collections.Frozen;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Holds the collection of telephony providers registered with the application.
/// </summary>
public sealed class TelephonyProviderOptions
{
    private readonly Dictionary<string, TelephonyProviderTypeOptions> _providers = new(StringComparer.OrdinalIgnoreCase);

    private FrozenDictionary<string, TelephonyProviderTypeOptions> _readonlyProviders;

    /// <summary>
    /// Gets the read-only collection of all registered telephony providers. The key is the technical
    /// name of the provider and the value describes the provider type and whether it is enabled. Lookups
    /// are case-insensitive, so <c>"Asterisk"</c> and <c>"asterisk"</c> resolve to the same provider.
    /// </summary>
    public IReadOnlyDictionary<string, TelephonyProviderTypeOptions> Providers
        => _readonlyProviders ??= _providers.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a provider when one with the same technical name does not already exist. Technical names are
    /// compared case-insensitively and trimmed, so registrations that differ only by case or surrounding
    /// whitespace are treated as the same provider. Re-registering the identical provider type is an
    /// idempotent no-op; registering a different provider type under an existing name is a configuration
    /// error and throws so the collision is observable at startup.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <param name="options">The type options of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a provider with the same technical name is already registered with a different provider type.</exception>
    public TelephonyProviderOptions TryAddProvider(string name, TelephonyProviderTypeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedName = NormalizeName(name);

        if (_providers.TryGetValue(normalizedName, out var existing))
        {
            if (existing.Type != options.Type)
            {
                throw new InvalidOperationException(
                    $"A telephony provider with the technical name '{normalizedName}' is already registered with a different provider type ('{existing.Type}'). Provider technical names must be unique; use '{nameof(ReplaceProvider)}' to intentionally override an existing registration.");
            }

            return this;
        }

        _providers.Add(normalizedName, options);
        _readonlyProviders = null;

        return this;
    }

    /// <summary>
    /// Removes a provider when one with the given technical name exists. The name is matched
    /// case-insensitively and trimmed.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or consists only of whitespace.</exception>
    public TelephonyProviderOptions RemoveProvider(string name)
    {
        var normalizedName = NormalizeName(name);

        if (_providers.Remove(normalizedName))
        {
            _readonlyProviders = null;
        }

        return this;
    }

    /// <summary>
    /// Replaces an existing provider or adds a new one. The name is matched case-insensitively and trimmed.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <param name="options">The type options of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or consists only of whitespace.</exception>
    public TelephonyProviderOptions ReplaceProvider(string name, TelephonyProviderTypeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedName = NormalizeName(name);

        RemoveProvider(normalizedName);

        return TryAddProvider(normalizedName, options);
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.Trim();
    }
}
