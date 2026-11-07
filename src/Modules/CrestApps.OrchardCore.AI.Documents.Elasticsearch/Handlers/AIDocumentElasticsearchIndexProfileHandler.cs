using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;

/// <summary>
/// Handles events for AI document elasticsearch index profile.
/// </summary>
public sealed class AIDocumentElasticsearchIndexProfileHandler : AIDocumentIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentElasticsearchIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The ai client factory.</param>
    /// <param name="logger">The logger.</param>
    public AIDocumentElasticsearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger<AIDocumentElasticsearchIndexProfileHandler> logger)
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
        metadata.IndexMappings.Mapping.Properties ??= new Elastic.Clients.Elasticsearch.Mapping.Properties();

        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);

        metadata.IndexMappings.KeyFieldName = DocumentIndexConstants.ColumnNames.ChunkId;
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.ChunkId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.DocumentId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.Content] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.FileName] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.ReferenceId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.ReferenceType] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.ChunkIndex] = new IntegerNumberProperty();
        metadata.IndexMappings.Mapping.Properties[DocumentIndexConstants.ColumnNames.Embedding] = new DenseVectorProperty
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
                DocumentIndexConstants.ColumnNames.Content,
            ];

            indexProfile.Put(metadata);
        }
    }
}
