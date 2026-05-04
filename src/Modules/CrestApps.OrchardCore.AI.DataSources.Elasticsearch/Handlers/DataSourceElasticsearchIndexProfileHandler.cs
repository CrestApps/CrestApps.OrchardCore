using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Elasticsearch;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;

internal sealed class DataSourceElasticsearchIndexProfileHandler : DataSourceIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceElasticsearchIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The ai client factory.</param>
    /// <param name="logger">The logger.</param>
    public DataSourceElasticsearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger<DataSourceElasticsearchIndexProfileHandler> logger)
    : base(ElasticsearchConstants.ProviderName, deploymentManager, aiClientFactory, logger)
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

        var queryMetadata = indexProfile.GetOrCreate<ElasticsearchDefaultQueryMetadata>();

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
