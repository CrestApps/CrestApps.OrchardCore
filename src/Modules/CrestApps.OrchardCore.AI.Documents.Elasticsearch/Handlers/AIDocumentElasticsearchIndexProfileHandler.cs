using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;
using CrestApps.OrchardCore.AI.Core;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;

public sealed class AIDocumentElasticsearchIndexProfileHandler : AIDocumentIndexProfileHandlerBase
{
    public AIDocumentElasticsearchIndexProfileHandler(
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
        metadata.IndexMappings.Mapping.Properties ??= new Elastic.Clients.Elasticsearch.Mapping.Properties();

        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);

        metadata.IndexMappings.KeyFieldName = AIConstants.ColumnNames.ChunkId;
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.ChunkId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.DocumentId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.Content] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.FileName] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.ReferenceId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.ReferenceType] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.ChunkIndex] = new IntegerNumberProperty();
        metadata.IndexMappings.Mapping.Properties[AIConstants.ColumnNames.Embedding] = new DenseVectorProperty
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
                AIConstants.ColumnNames.Content,
            ];

            indexProfile.Put(metadata);
        }
    }
}
