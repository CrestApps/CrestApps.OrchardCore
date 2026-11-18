using System.Text.Json;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.SigningKeys;

/// <summary>
/// Keeps secrets in memory as Orchard Core's database store does: each one serialized on save and read back as a new
/// object, so a test sees only what was saved.
/// </summary>
internal sealed class InMemorySecretManager : ISecretManager
{
    private readonly Dictionary<string, (Type Type, string Json, SecretSaveOptions Options)> _secrets = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Names => _secrets.Keys;

    public int Saves { get; private set; }

    public Type TypeOf(string name)
        => _secrets.TryGetValue(name, out var entry) ? entry.Type : null;

    public SecretSaveOptions OptionsOf(string name)
        => _secrets.TryGetValue(name, out var entry) ? entry.Options : null;

    public Task<Secret> GetSecretAsync(string name)
        => Task.FromResult(_secrets.TryGetValue(name, out var entry) ? Secret.FromValue(JsonSerializer.Deserialize(entry.Json, entry.Type)) : default);

    public Task<Secret> GetSecretAsync(string name, string storeName)
        => GetSecretAsync(name);

    public Task SaveSecretAsync(string name, Secret secret, SecretSaveOptions options = null)
    {
        var value = secret.Value ?? throw new ArgumentException("The secret has no value.", nameof(secret));

        _secrets[name] = (value.GetType(), JsonSerializer.Serialize(value, value.GetType()), options);
        Saves++;

        return Task.CompletedTask;
    }

    public Task SaveSecretAsync(string name, Secret secret, string storeName, SecretSaveOptions options = null)
        => SaveSecretAsync(name, secret, options);

    public Task RemoveSecretAsync(string name)
    {
        _secrets.Remove(name);

        return Task.CompletedTask;
    }

    public Task RemoveSecretAsync(string name, string storeName)
        => RemoveSecretAsync(name);

    public Task<IEnumerable<SecretInfo>> GetSecretInfosAsync()
        => Task.FromResult<IEnumerable<SecretInfo>>(_secrets.Select(pair => new SecretInfo { Name = pair.Key, Type = pair.Value.Type.FullName }).ToArray());

    public IEnumerable<ISecretStore> GetStores()
        => [];
}
