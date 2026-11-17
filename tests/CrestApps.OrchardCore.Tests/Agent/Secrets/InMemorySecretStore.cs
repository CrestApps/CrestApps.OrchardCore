using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.Agent.Secrets;

/// <summary>
/// A secret store kept in memory for a tool test. It records each read of a value and each save, so a test can tell
/// what a tool did with the secrets.
/// </summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, (Secret Secret, SecretInfo Info)> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public InMemorySecretStore(string name, bool isReadOnly)
    {
        Name = name;
        IsReadOnly = isReadOnly;
    }

    public string Name { get; }

    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets the names of the secrets whose value was read, in order.
    /// </summary>
    public List<string> ValueReads { get; } = [];

    /// <summary>
    /// Gets the names of the secrets that were saved, in order.
    /// </summary>
    public List<string> Saves { get; } = [];

    /// <summary>
    /// Puts a secret in the store, as the fixture has it before a tool runs.
    /// </summary>
    /// <param name="name">The name of the secret.</param>
    /// <param name="value">The secret, an instance of one of the secret types.</param>
    /// <param name="description">The description of the secret.</param>
    /// <param name="expiresUtc">When the secret expires.</param>
    public void Add(string name, object value, string description, DateTime? expiresUtc)
    {
        _secrets[name] = (Secret.FromValue(value), new SecretInfo
        {
            Name = name,
            Store = Name,
            Type = value.GetType().FullName,
            Description = description,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresUtc = expiresUtc,
        });
    }

    /// <summary>
    /// Gets what the store keeps of a secret without counting as a read by a tool.
    /// </summary>
    /// <param name="name">The name of the secret.</param>
    /// <returns>The stored secret, or <see langword="null"/> when the store has none with the name.</returns>
    public object Peek(string name)
    {
        return _secrets.TryGetValue(name, out var entry)
            ? entry.Secret.Value
            : null;
    }

    /// <summary>
    /// Gets the information the store keeps about a secret.
    /// </summary>
    /// <param name="name">The name of the secret.</param>
    /// <returns>The information, or <see langword="null"/> when the store has none with the name.</returns>
    public SecretInfo PeekInfo(string name)
    {
        return _secrets.TryGetValue(name, out var entry)
            ? entry.Info
            : null;
    }

    public bool Contains(string name)
        => _secrets.ContainsKey(name);

    public void MoveTo(InMemorySecretStore destination, string name)
    {
        var (secret, info) = _secrets[name];

        info.Store = destination.Name;
        destination._secrets[name] = (secret, info);
        _secrets.Remove(name);
    }

    public Task<Secret> GetSecretAsync(string name)
    {
        ValueReads.Add(name);

        return Task.FromResult(_secrets.TryGetValue(name, out var entry)
            ? entry.Secret
            : default);
    }

    public Task SaveSecretAsync(string name, Secret secret, SecretSaveOptions options = null)
    {
        Saves.Add(name);

        var existing = _secrets.TryGetValue(name, out var entry)
            ? entry.Info
            : null;

        _secrets[name] = (secret, new SecretInfo
        {
            Name = name,
            Store = Name,
            Type = secret.Value.GetType().FullName,
            Description = options is null
                ? existing?.Description
                : options.Description,
            CreatedUtc = existing?.CreatedUtc ?? SecretToolDoubles.UtcNow,
            UpdatedUtc = SecretToolDoubles.UtcNow,
            ExpiresUtc = options is null
                ? existing?.ExpiresUtc
                : options.ExpiresUtc,
        });

        return Task.CompletedTask;
    }

    public Task RemoveSecretAsync(string name)
    {
        _secrets.Remove(name);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<SecretInfo>> GetSecretInfosAsync()
        => Task.FromResult<IEnumerable<SecretInfo>>(_secrets.Values.Select(entry => entry.Info).ToList());
}
