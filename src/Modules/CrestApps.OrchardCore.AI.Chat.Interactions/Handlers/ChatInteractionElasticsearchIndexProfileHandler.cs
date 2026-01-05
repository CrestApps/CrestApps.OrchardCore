using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;
using OrchardCore.Search.Elasticsearch.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;


public sealed class ChatInteractionElasticsearchIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IAIClientFactory _aiClientFactory;

    public ChatInteractionElasticsearchIndexProfileHandler(
        IAIClientFactory aiClientFactory)
    {
        _aiClientFactory = aiClientFactory;
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];
        metadata.IndexMappings.Mapping.Meta ??= new Dictionary<string, object>();

        // Get embedding connection from index profile metadata
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        // Dynamically determine embedding dimensions by generating a sample embedding using the configured connection
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(interactionMetadata);

        metadata.IndexMappings.KeyFieldName = ChatInteractionsConstants.ColumnNames.DocumentId;
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.DocumentId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.InteractionId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.FileName] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.Chunks] = new NestedProperty()
        {
            Properties = new Properties()
            {
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, new TextProperty() },
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Embedding, new DenseVectorProperty
                    {
                        Dims = embeddingDimensions,
                        Index = true,
                    }
                },
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, new IntegerNumberProperty() },
            },
        };

        indexProfile.Put(metadata);
    }

    private async Task<int> GetEmbeddingDimensionsAsync(ChatInteractionIndexProfileMetadata interactionMetadata)
    {
        // Default to 1536 (OpenAI text-embedding-ada-002) if we can't determine dynamically
        const int defaultDimensions = 1536;

        // Use the embedding connection configured in the index profile
        if (string.IsNullOrEmpty(interactionMetadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingDeploymentName))
        {
            return defaultDimensions;
        }

        try
        {
            var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
                interactionMetadata.EmbeddingProviderName,
                interactionMetadata.EmbeddingConnectionName,
                interactionMetadata.EmbeddingDeploymentName);

            if (embeddingGenerator == null)
            {
                return defaultDimensions;
            }

            // Generate embedding for a sample text to determine dimensions
            var embedding = await embeddingGenerator.GenerateAsync(["Sample"]);

            if (embedding?.Count > 0 && embedding[0].Vector.Length > 0)
            {
                return embedding[0].Vector.Length;
            }
        }
        catch (Exception)
        {
            // If we can't determine dimensions dynamically (e.g., invalid connection or API error),
            // silently fall back to default dimensions.
        }

        return defaultDimensions;
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(ElasticsearchConstants.ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
