using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Infrastructure;

namespace CrestApps.Mvc.Web.Areas.AI.Services;


/// <summary>
/// Holds the MVC sample's runtime projection of stored AI provider connections.
/// The snapshot is loaded during startup and refreshed after connection changes
/// so <see cref="AIProviderOptions"/> can be rebuilt without querying YesSql
/// inside the options pipeline.
/// </summary>
public sealed class MvcAIProviderOptionsStore
{
    private readonly object _syncLock = new();

    private Dictionary<string, AIProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public void Replace(IEnumerable<AIProviderConnection> connections)
    {
        var providers = new Dictionary<string, AIProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in connections.GroupBy(static connection => AIProviderNameNormalizer.Normalize(connection.ClientName)))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            var provider = new AIProvider
            {
                Connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase),
            };

            var defaultConnection = group.FirstOrDefault();

            foreach (var connection in group)
            {
                if (string.IsNullOrWhiteSpace(connection.Name))
                {
                    continue;
                }

                var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                if (connection.Properties is not null)
                {
                    foreach (var property in connection.Properties)
                    {
                        values[property.Key] = property.Value;
                    }
                }

#pragma warning disable CS0618
                values["ChatDeploymentName"] = connection.ChatDeploymentName;
                values["EmbeddingDeploymentName"] = connection.EmbeddingDeploymentName;
                values["UtilityDeploymentName"] = connection.UtilityDeploymentName;
                values["ImagesDeploymentName"] = connection.ImagesDeploymentName;
                values["SpeechToTextDeploymentName"] = connection.SpeechToTextDeploymentName;
#pragma warning restore CS0618
                values["ConnectionNameAlias"] = connection.Name;

                provider.Connections[connection.Name] = new AIProviderConnectionEntry(values);
            }

            if (provider.Connections.Count == 0)
            {
                continue;
            }

#pragma warning disable CS0618
            provider.DefaultChatDeploymentName = defaultConnection?.ChatDeploymentName
            ?? provider.Connections.First().Value?.GetChatDeploymentOrDefaultName(false);
            provider.DefaultEmbeddingDeploymentName = defaultConnection?.EmbeddingDeploymentName;
            provider.DefaultImagesDeploymentName = defaultConnection?.ImagesDeploymentName;
            provider.DefaultUtilityDeploymentName = defaultConnection?.UtilityDeploymentName;
#pragma warning restore CS0618

            providers[group.Key] = provider;
        }

        lock (_syncLock)
        {
            _providers = providers;
        }
    }

    public void ApplyTo(AIProviderOptions options)
    {
        Dictionary<string, AIProvider> providers;

        lock (_syncLock)
        {
            providers = new Dictionary<string, AIProvider>(_providers, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var provider in providers)
        {
            var targetProvider = AIProviderOptionsConnectionMerger.GetOrAddProvider(options, provider.Key);

#pragma warning disable CS0618 // Obsolete deployment name fields retained for backward compatibility
            targetProvider.DefaultChatDeploymentName ??= provider.Value.DefaultChatDeploymentName;
            targetProvider.DefaultEmbeddingDeploymentName ??= provider.Value.DefaultEmbeddingDeploymentName;
            targetProvider.DefaultImagesDeploymentName ??= provider.Value.DefaultImagesDeploymentName;
            targetProvider.DefaultUtilityDeploymentName ??= provider.Value.DefaultUtilityDeploymentName;
#pragma warning restore CS0618

            foreach (var connection in provider.Value.Connections)
            {
                AIProviderOptionsConnectionMerger.MergeConnection(targetProvider, connection.Key, connection.Value);
            }
        }
    }
}
