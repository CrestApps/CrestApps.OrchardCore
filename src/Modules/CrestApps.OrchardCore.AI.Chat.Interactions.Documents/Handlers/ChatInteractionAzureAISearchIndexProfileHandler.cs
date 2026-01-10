using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;

public sealed class ChatInteractionAzureAISearchIndexProfileHandler : ChatInteractionsIndexProfileHandlerBase
{
    public ChatInteractionAzureAISearchIndexProfileHandler(IAIClientFactory aiClientFactory)
        : base(AzureAISearchConstants.ProviderName, aiClientFactory)
    {
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
            SubFields =
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
}
