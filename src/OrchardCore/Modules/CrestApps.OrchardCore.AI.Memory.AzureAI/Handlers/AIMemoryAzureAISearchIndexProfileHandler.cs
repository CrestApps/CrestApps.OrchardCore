using CrestApps.AI.Clients;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.OrchardCore.AI.Memory.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.Memory.AzureAI.Handlers;

public sealed class AIMemoryAzureAISearchIndexProfileHandler : AIMemoryIndexProfileHandlerBase
{
    public AIMemoryAzureAISearchIndexProfileHandler(IAIClientFactory aiClientFactory)
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
        var memoryMetadata = indexProfile.As<AIMemoryIndexProfileMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(memoryMetadata);
        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.MemoryId,
            Type = DocumentIndex.Types.Text,
            IsKey = true,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.UserId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.Name,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.Description,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.Content,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.UpdatedUtc,
            Type = DocumentIndex.Types.DateTime,
            IsFilterable = true,
            IsSortable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = MemoryConstants.ColumnNames.Embedding,
            Type = DocumentIndex.Types.Number,
            VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = embeddingDimensions,
            },
        });

        indexProfile.Put(metadata);
    }
}
