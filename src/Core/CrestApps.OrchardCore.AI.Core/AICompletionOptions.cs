using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

public sealed class AICompletionOptions
{
    private readonly Dictionary<string, Type> _clients = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AIProfileProviderEntry> _profileSources = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AIDeploymentProviderEntry> _deployments = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AIProviderConnectionOptionsEntry> _connectionSources = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Type> Clients => _clients;

    public IReadOnlyDictionary<string, AIProfileProviderEntry> ProfileSources => _profileSources;

    public IReadOnlyDictionary<string, AIDeploymentProviderEntry> Deployments => _deployments;

    public IReadOnlyDictionary<string, AIProviderConnectionOptionsEntry> ConnectionSources => _connectionSources;

    internal void AddClient<TClient>(string name)
        where TClient : class, IAICompletionClient
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        _clients[name] = typeof(TClient);
    }

    internal void AddProfileSource(string name, string providerName, Action<AIProfileProviderEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!_profileSources.TryGetValue(name, out var entry))
        {
            entry = new AIProfileProviderEntry(providerName);
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(providerName, providerName);
        }

        _profileSources[name] = entry;
    }

    public void AddDeploymentProvider(string providerName, Action<AIDeploymentProviderEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        if (!_deployments.TryGetValue(providerName, out var entry))
        {
            entry = new AIDeploymentProviderEntry();
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(providerName, providerName);
        }

        _deployments[providerName] = entry;
    }

    public void AddConnectionSource(string providerName, Action<AIProviderConnectionOptionsEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        if (!_connectionSources.TryGetValue(providerName, out var entry))
        {
            entry = new AIProviderConnectionOptionsEntry(providerName);
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(providerName, providerName);
        }

        _connectionSources[providerName] = entry;
    }
}

public sealed class AIDeploymentProviderEntry
{
    public string DisplayName { get; set; }

    public string Description { get; set; }
}

public sealed class AIProfileProviderEntry
{
    public AIProfileProviderEntry(string providerName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public string DisplayName { get; set; }

    public string Description { get; set; }
}

public sealed class AIProviderConnectionOptionsEntry
{
    public AIProviderConnectionOptionsEntry(string providerName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public string DisplayName { get; set; }

    public string Description { get; set; }
}
