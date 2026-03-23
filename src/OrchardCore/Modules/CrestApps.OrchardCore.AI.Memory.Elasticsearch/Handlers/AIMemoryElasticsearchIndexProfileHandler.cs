using CrestApps.AI;
using CrestApps.OrchardCore.AI.Memory.Handlers;
using CrestApps.Handlers;
using CrestApps.OrchardCore.AI.Memory.Models;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;
using OrchardCore.Search.Elasticsearch.Models;

namespace CrestApps.OrchardCore.AI.Memory.Elasticsearch.Handlers;

public sealed class AIMemoryElasticsearchIndexProfileHandler : AIMemoryIndexProfileHandlerBase
{
    public AIMemoryElasticsearchIndexProfileHandler(IAIClientFactory aiClientFactory)
        : base(ElasticsearchConstants.ProviderName, aiClientFactory)
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

        var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= new Elastic.Clients.Elasticsearch.Mapping.Properties();

        var profileMetadata = indexProfile.As<AIMemoryIndexProfileMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(profileMetadata);

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

        var metadata = indexProfile.As<ElasticsearchDefaultQueryMetadata>();

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
