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

    public Task<T> GetSecretAsync<T>(string name)
        where T : class, ISecret
        => Task.FromResult(_secrets.TryGetValue(name, out var entry) ? JsonSerializer.Deserialize(entry.Json, entry.Type) as T : null);

    public Task<T> GetSecretAsync<T>(string name, string storeName)
        where T : class, ISecret
        => GetSecretAsync<T>(name);

    public Task SaveSecretAsync<T>(string name, T secret, SecretSaveOptions options = null)
        where T : class, ISecret
    {
        _secrets[name] = (secret.GetType(), JsonSerializer.Serialize(secret, secret.GetType()), options);
        Saves++;

        return Task.CompletedTask;
    }

    public Task SaveSecretAsync<T>(string name, T secret, string storeName, SecretSaveOptions options = null)
        where T : class, ISecret
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
