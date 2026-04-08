using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
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

        foreach (var connection in document.Records.Values)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(connection.ClientName) ||
                    string.IsNullOrWhiteSpace(connection.ItemId))
                {
                    continue;
                }

                var mappingContext = new InitializingAIProviderConnectionContext(connection);

#pragma warning disable CS0618 // Obsolete deployment name fields retained for backward compatibility
                mappingContext.Values["ChatDeploymentName"] = connection.ChatDeploymentName;
                mappingContext.Values["EmbeddingDeploymentName"] = connection.EmbeddingDeploymentName;
                mappingContext.Values["UtilityDeploymentName"] = connection.UtilityDeploymentName;
                mappingContext.Values["ImagesDeploymentName"] = connection.ImagesDeploymentName;
                mappingContext.Values["SpeechToTextDeploymentName"] = connection.SpeechToTextDeploymentName;
#pragma warning restore CS0618
                mappingContext.Values["ConnectionNameAlias"] = connection.Name;

                _handlers.Invoke((handler, ctx) => handler.Initializing(ctx), mappingContext, _logger);

                AIProviderOptionsConnectionMerger.MergeConnection(
                    options,
                    connection.ClientName,
                    connection.ItemId,
                    new AIProviderConnectionEntry(mappingContext.Values));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure AI provider connection '{ConnectionId}' for provider '{ProviderName}' from stored connections. This may occur if the stored connection data is invalid or uses an outdated format. Please review the provider connection settings.", connection.ItemId, connection.ClientName);
            }
        }
    }
}
