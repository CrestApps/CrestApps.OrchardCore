using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;

public sealed class AIDocumentAzureAISearchIndexProfileHandler : AIDocumentIndexProfileHandlerBase
{
    public AIDocumentAzureAISearchIndexProfileHandler(IAIClientFactory aiClientFactory)
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
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(interactionMetadata);

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.ChunkId,
            Type = DocumentIndex.Types.Text,
            IsKey = true,
            IsFilterable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.DocumentId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.Content,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.FileName,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.ReferenceId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.ReferenceType,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.ChunkIndex,
            Type = DocumentIndex.Types.Integer,
        });
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = AIConstants.ColumnNames.Embedding,
            Type = DocumentIndex.Types.Number,
            VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = embeddingDimensions,
            },
        });

        indexProfile.Put(metadata);
    }
}