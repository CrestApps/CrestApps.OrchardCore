using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.Memory.Elasticsearch.Handlers;

public sealed class AIMemoryElasticsearchIndexProfileHandler : AIMemoryIndexProfileHandlerBase
{
    public AIMemoryElasticsearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    : base(ElasticsearchConstants.ProviderName, deploymentManager, aiClientFactory)
    {
    }

    public override async Task InitializingAsync(InitializingContext<IndexProfile> context)
    {
        await SetMappingAsync(context.Model);
        SetDefaultSearchFields(context.Model);
    }

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

        var metadata = indexProfile.GetOrCreate<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];

        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);

        metadata.IndexMappings.KeyFieldName = MemoryConstants.ColumnNames.MemoryId;
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.MemoryId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.UserId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.Name] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.Description] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.Content] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.UpdatedUtc] = new DateProperty();
        metadata.IndexMappings.Mapping.Properties[MemoryConstants.ColumnNames.Embedding] = new DenseVectorProperty
        {
            Dims = embeddingDimensions,
            Index = true,
            Similarity = DenseVectorSimilarity.Cosine,
        };

        indexProfile.Put(metadata);
    }

    private void SetDefaultSearchFields(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.GetOrCreate<ElasticsearchDefaultQueryMetadata>();

        if (metadata.DefaultSearchFields is null || metadata.DefaultSearchFields.Length == 0)
        {
            metadata.DefaultSearchFields =
            [
                MemoryConstants.ColumnNames.Name,
                MemoryConstants.ColumnNames.Description,
                MemoryConstants.ColumnNames.Content,
            ];

            indexProfile.Put(metadata);
        }
    }
}
