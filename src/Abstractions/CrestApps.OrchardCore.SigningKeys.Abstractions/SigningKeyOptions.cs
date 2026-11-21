namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// The key sets the tenant's features registered with <c>AddSigningKeySet</c>.
/// </summary>
public sealed class SigningKeyOptions
{
    private readonly Dictionary<string, SigningKeySetOptions> _keySets = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the registered key sets by name.
    /// </summary>
    public IReadOnlyDictionary<string, SigningKeySetOptions> KeySets => _keySets;

    /// <summary>
    /// Gets a key set, adding it when it is not registered yet.
    /// </summary>
    /// <param name="name">The name of the key set.</param>
    /// <returns>The key set.</returns>
    public SigningKeySetOptions GetOrAdd(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!_keySets.TryGetValue(name, out var keySet))
        {
            keySet = new SigningKeySetOptions(name);
            _keySets[name] = keySet;
        }

        return keySet;
    }
}
