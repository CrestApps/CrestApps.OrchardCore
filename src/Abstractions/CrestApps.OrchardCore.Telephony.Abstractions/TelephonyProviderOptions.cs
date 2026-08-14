using System.Collections.Frozen;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Holds the collection of telephony providers registered with the application.
/// </summary>
public sealed class TelephonyProviderOptions
{
    private readonly Dictionary<string, TelephonyProviderTypeOptions> _providers = [];

    private FrozenDictionary<string, TelephonyProviderTypeOptions> _readonlyProviders;

    /// <summary>
    /// Gets the read-only collection of all registered telephony providers. The key is the technical
    /// name of the provider and the value describes the provider type and whether it is enabled.
    /// </summary>
    public IReadOnlyDictionary<string, TelephonyProviderTypeOptions> Providers
        => _readonlyProviders ??= _providers.ToFrozenDictionary(entry => entry.Key, entry => entry.Value);

    /// <summary>
    /// Adds a provider when one with the same technical name does not already exist.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <param name="options">The type options of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/> or empty.</exception>
    public TelephonyProviderOptions TryAddProvider(string name, TelephonyProviderTypeOptions options)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
        }

        if (_providers.ContainsKey(name))
        {
            return this;
        }

        _providers.Add(name, options);
        _readonlyProviders = null;

        return this;
    }

    /// <summary>
    /// Removes a provider when one with the given technical name exists.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/> or empty.</exception>
    public TelephonyProviderOptions RemoveProvider(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
        }

        if (_providers.Remove(name))
        {
            _readonlyProviders = null;
        }

        return this;
    }

    /// <summary>
    /// Replaces an existing provider or adds a new one.
    /// </summary>
    /// <param name="name">The technical name of the provider.</param>
    /// <param name="options">The type options of the provider.</param>
    /// <returns>The current <see cref="TelephonyProviderOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/> or empty.</exception>
    public TelephonyProviderOptions ReplaceProvider(string name, TelephonyProviderTypeOptions options)
    {
        _providers.Remove(name);

        return TryAddProvider(name, options);
    }
}
