using Azure.Search.Documents.Indexes.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

public sealed class ChatInteractionAzureAISearchIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IAIClientFactory _aiClientFactory;

    public ChatInteractionAzureAISearchIndexProfileHandler(
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

        var metadata = indexProfile.As<AzureAISearchIndexMetadata>();

        metadata.IndexMappings ??= [];

        // Get embedding connection from index profile metadata
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        // Dynamically determine embedding dimensions by generating a sample embedding using the configured connection
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(interactionMetadata);

        // Define the document ID field as the key field
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = ChatInteractionsConstants.ColumnNames.DocumentId,
            Type = DocumentIndex.Types.Text,
            IsKey = true,
            IsFilterable = true,
        });

        // Define the text field as a searchable field
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = ChatInteractionsConstants.ColumnNames.Text,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        // Define the interaction ID field for filtering
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = ChatInteractionsConstants.ColumnNames.InteractionId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        // Define the file name field
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = ChatInteractionsConstants.ColumnNames.FileName,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        // Define the chunks field as a complex collection type
        // In Azure AI Search, complex types are used for nested objects
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = ChatInteractionsConstants.ColumnNames.Chunks,
            Type = DocumentIndex.Types.Complex,
            Fields =
            [
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text,
                    Type = DocumentIndex.Types.Text,
                    IsSearchable = true,
                },
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Embedding,
                    Type = DocumentIndex.Types.Number,
                    VectorInfo = new AzureAISearchIndexMapVectorInfo
                    {
                        Dimensions = embeddingDimensions,
                        IsCollection = true,
                    },
                },
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index,
                    Type = DocumentIndex.Types.Integer,
                },
            ]
        });

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
        return string.Equals(AzureAISearchConstants.ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
