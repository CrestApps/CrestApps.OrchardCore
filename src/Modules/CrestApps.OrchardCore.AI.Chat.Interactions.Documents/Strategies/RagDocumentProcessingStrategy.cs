using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Strategies;

/// <summary>
/// Strategy for handling Document Q&amp;A (RAG) requests.
/// Uses vector search to find relevant document chunks and inject them as context.
/// </summary>
public sealed class RagDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly IOptions<AIProviderOptions> _providerOptions;
    private readonly ILogger<RagDocumentProcessingStrategy> _logger;

    public RagDocumentProcessingStrategy(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aIClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<RagDocumentProcessingStrategy> logger)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _aIClientFactory = aIClientFactory;
        _providerOptions = providerOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(DocumentProcessingContext context)
    {
        if (!string.Equals(context.IntentResult?.Intent, DocumentIntents.DocumentQnA, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var interaction = context.Interaction;
        var prompt = context.Prompt;
        var cancellationToken = context.CancellationToken;

        // Check if there are documents attached
        if (interaction.Documents == null || interaction.Documents.Count == 0)
        {
            return;
        }

        try
        {
            // Get the document settings to find the index profile
            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();

            if (string.IsNullOrEmpty(settings.IndexProfileName))
            {
                _logger.LogWarning("Documents are attached but no index profile is configured. Document context will not be used.");
                return;
            }

            // Find the index profile
            var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile == null)
            {
                _logger.LogWarning("Index profile '{IndexProfileName}' not found. Document context will not be used.", settings.IndexProfileName);
                return;
            }

            // Get the embedding search service for this provider
            var searchService = context.ServiceProvider?.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);

            if (searchService == null)
            {
                _logger.LogWarning("No embedding search service registered for provider '{ProviderName}'. Document context will not be used.", indexProfile.ProviderName);
                return;
            }

            // Get embedding for the user's prompt
            var providerName = interaction.Source;
            var connectionName = interaction.ConnectionName;
            string deploymentName = null;

            if (_providerOptions.Value.Providers.TryGetValue(providerName, out var provider))
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = provider.DefaultConnectionName;
                }

                if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connection))
                {
                    deploymentName = connection.GetDefaultEmbeddingDeploymentName(false);
                }
            }

            if (string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("No embedding deployment configured. Document context will not be used.");
                return;
            }

            var embeddingGenerator = await _aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

            if (embeddingGenerator == null)
            {
                _logger.LogWarning("Failed to create embedding generator. Document context will not be used.");
                return;
            }

            // Generate embedding for the prompt
            var embeddings = await embeddingGenerator.GenerateAsync([prompt], cancellationToken: cancellationToken);

            if (embeddings == null || embeddings.Count == 0 || embeddings[0]?.Vector == null || embeddings[0].Vector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for prompt. Document context will not be used.");
                return;
            }

            var embedding = embeddings[0];

            // Search for similar document chunks
            var topN = interaction.DocumentTopN ?? settings.TopN;

            if (topN <= 0)
            {
                topN = 3;
            }

            var results = await searchService.SearchAsync(
                indexProfile,
                embedding.Vector.ToArray(),
                interaction.ItemId,
                topN,
                cancellationToken);

            if (results == null || !results.Any())
            {
                return;
            }

            // Combine the relevant chunks into context
            var contextBuilder = new StringBuilder();

            foreach (var result in results)
            {
                if (result.Chunk != null && !string.IsNullOrWhiteSpace(result.Chunk.Text))
                {
                    contextBuilder.AppendLine("---");
                    contextBuilder.AppendLine(result.Chunk.Text);
                }
            }

            var documentContext = contextBuilder.ToString();

            if (string.IsNullOrWhiteSpace(documentContext))
            {
                return;
            }

            context.Result.AddContext(
                documentContext,
                "The following is relevant context from uploaded documents. Use this information to answer the user's question:",
                usedVectorSearch: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document context. Document context will not be used.");
        }
    }
}
