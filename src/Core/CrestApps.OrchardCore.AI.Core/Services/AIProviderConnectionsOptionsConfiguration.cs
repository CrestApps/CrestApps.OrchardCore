using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIProviderConnectionsOptionsConfiguration : IConfigureOptions<AIProviderOptions>
{
    private readonly IDocumentManager<DictionaryDocument<AIProviderConnection>> _documentManager;
    private readonly IEnumerable<IAIProviderConnectionHandler> _handlers;

    private readonly ILogger _logger;

    public AIProviderConnectionsOptionsConfiguration(
        IDocumentManager<DictionaryDocument<AIProviderConnection>> documentManager,
        IEnumerable<IAIProviderConnectionHandler> handlers,
        ILogger<AIProviderConnectionsOptionsConfiguration> logger)
    {
        _documentManager = documentManager;
        _handlers = handlers;
        _logger = logger;
    }

    public void Configure(AIProviderOptions options)
    {
        DictionaryDocument<AIProviderConnection> document;

        try
        {
            document = _documentManager.GetOrCreateMutableAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load AI provider connections from the database. This may occur if the module migrations have not yet been applied or the data is corrupted.");

            return;
        }

        if (document.Records.Count == 0)
        {
            return;
        }

        var groups = document.Records.Values
            .GroupBy(x => x.ClientName)
            .Select(x => new
            {
                ProviderName = x.Key,
                Connections = x,
            });

        foreach (var group in groups)
        {
            try
            {
                if (!options.Providers.TryGetValue(group.ProviderName, out var provider))
                {
                    provider = new AIProvider()
                    {
                        Connections = new Dictionary<string, AIProviderConnectionEntry>(),
                    };
                }

                AIProviderConnection defaultConnection = null;

                foreach (var connection in group.Connections)
                {
                    var mappingContext = new InitializingAIProviderConnectionContext(connection);

                    if (defaultConnection is null && connection.IsDefault)
                    {
                        defaultConnection = connection;
                    }

                    if (string.IsNullOrEmpty(connection.ItemId))
                    {
                        continue;
                    }

#pragma warning disable CS0618 // Obsolete deployment name fields retained for backward compatibility
                    mappingContext.Values["ChatDeploymentName"] = connection.ChatDeploymentName;
                    mappingContext.Values["EmbeddingDeploymentName"] = connection.EmbeddingDeploymentName;
                    mappingContext.Values["UtilityDeploymentName"] = connection.UtilityDeploymentName;
                    mappingContext.Values["ImagesDeploymentName"] = connection.ImagesDeploymentName;
                    mappingContext.Values["SpeechToTextDeploymentName"] = connection.SpeechToTextDeploymentName;
#pragma warning restore CS0618
                    mappingContext.Values["ConnectionNameAlias"] = connection.Name;

                    _handlers.Invoke((handler, ctx) => handler.Initializing(ctx), mappingContext, _logger);

                    provider.Connections[connection.ItemId] = new AIProviderConnectionEntry(mappingContext.Values);
                }

#pragma warning disable CS0618 // Obsolete deployment name fields retained for backward compatibility
                if (defaultConnection is not null)
                {
                    provider.DefaultConnectionName = defaultConnection.ItemId;
                    provider.DefaultChatDeploymentName = defaultConnection.ChatDeploymentName;
                }
                else
                {
                    if (string.IsNullOrEmpty(provider.DefaultChatDeploymentName) && provider.Connections.Count > 0)
                    {
                        provider.DefaultChatDeploymentName = provider.Connections.First().Value?.GetChatDeploymentOrDefaultName(false);
                    }
#pragma warning restore CS0618

                    if (string.IsNullOrEmpty(provider.DefaultConnectionName) && provider.Connections.Count > 0)
                    {
                        provider.DefaultConnectionName = provider.Connections.First().Key;
                    }
                }

                options.Providers[group.ProviderName] = provider;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure AI provider '{ProviderName}' from stored connections. The provider will be skipped. This may occur if the stored connection data is invalid or uses an outdated format. Please review the provider connection settings.", group.ProviderName);
            }
        }
    }
}
