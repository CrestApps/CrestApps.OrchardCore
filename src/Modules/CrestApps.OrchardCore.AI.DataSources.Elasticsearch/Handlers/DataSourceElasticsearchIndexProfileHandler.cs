using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;
using OrchardCore.Search.Elasticsearch.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;

internal sealed class DataSourceElasticsearchIndexProfileHandler : DataSourceIndexProfileHandlerBase
{
    public DataSourceElasticsearchIndexProfileHandler(IAIClientFactory aiClientFactory)
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
        metadata.IndexMappings.Mapping.Properties ??= [];

        var profileMetadata = indexProfile.As<DataSourceIndexProfileMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(profileMetadata);

        metadata.IndexMappings.KeyFieldName = DataSourceConstants.ColumnNames.ChunkId;
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.ChunkId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.ReferenceId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.DataSourceId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.ReferenceType] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.ChunkIndex] = new IntegerNumberProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.Title] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.Content] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.Timestamp] = new DateProperty();
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.Embedding] = new DenseVectorProperty
        {
            Dims = embeddingDimensions,
            Index = true,
            Similarity = DenseVectorSimilarity.Cosine,
        };
        metadata.IndexMappings.Mapping.Properties[DataSourceConstants.ColumnNames.Filters] = new ObjectProperty
        {
            Dynamic = DynamicMapping.True,
        };

        indexProfile.Put(metadata);
    }

    private void SetDefaultSearchFields(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var queryMetadata = indexProfile.As<ElasticsearchDefaultQueryMetadata>();

        if (queryMetadata.DefaultSearchFields is null || queryMetadata.DefaultSearchFields.Length == 0)
        {
            queryMetadata.DefaultSearchFields =
            [
                DataSourceConstants.ColumnNames.Content,
            ];

            indexProfile.Put(queryMetadata);
        }
    }
}
