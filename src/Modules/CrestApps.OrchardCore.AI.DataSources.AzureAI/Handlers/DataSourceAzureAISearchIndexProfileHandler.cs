using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Microsoft.Extensions.Logging;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;

internal sealed class DataSourceAzureAISearchIndexProfileHandler : DataSourceIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceAzureAISearchIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The ai client factory.</param>
    /// <param name="logger">The logger.</param>
    public DataSourceAzureAISearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger<DataSourceAzureAISearchIndexProfileHandler> logger)
        : base(AzureAISearchConstants.ProviderName, deploymentManager, aiClientFactory, logger)
    {
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task LoadedAsync(LoadedContext<IndexProfile> context)
    {
        if (!CanHandle(context.Model))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.GetOrCreate<AzureAISearchIndexMetadata>();

        foreach (var map in metadata.IndexMappings)
        {
            map.IndexingKey ??= map.AzureFieldKey;
        }

        context.Model.Put(metadata);

        return Task.CompletedTask;
    }

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);
        metadata.IndexMappings.Clear();

        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.ChunkId, DocumentIndex.Types.Text, map =>
        {
            map.IsKey = true;
            map.IsFilterable = true;
        }));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.ReferenceId, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.DataSourceId, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.ReferenceType, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.ChunkIndex, DocumentIndex.Types.Integer));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.Title, DocumentIndex.Types.Text, map => map.IsSearchable = true));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.Content, DocumentIndex.Types.Text, map => map.IsSearchable = true));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.Timestamp, DocumentIndex.Types.DateTime, map =>
        {
            map.IsFilterable = true;
            map.IsSortable = true;
        }));
        metadata.IndexMappings.Add(CreateIndexMap(DataSourceConstants.ColumnNames.Embedding, DocumentIndex.Types.Vector, map =>
        {
            map.IsSearchable = true;
            map.VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = embeddingDimensions,
                VectorSearchConfiguration = "default",
            };
        }));

        metadata.VectorSearchMappings = new VectorSearchMappings
        {
            Profiles =
            [
                new VectorSearchProfileMap
                {
                    Name = "default",
                    AlgorithmConfigurationName = "default-hnsw",
                },
            ],
            Algorithms =
            [
                new VectorSearchAlgorithmMap
                {
                    Name = "default-hnsw",
                    Kind = VectorSearchAlgorithmMap.HnswKind,
                },
            ],
        };

        indexProfile.Put(metadata);
    }

    private static AzureAISearchIndexMap CreateIndexMap(
        string fieldKey,
        DocumentIndex.Types type,
        Action<AzureAISearchIndexMap> configure = null)
    {
        var indexMap = new AzureAISearchIndexMap
        {
            IndexingKey = fieldKey,
            AzureFieldKey = fieldKey,
            Type = type,
        };

        configure?.Invoke(indexMap);

        return indexMap;
    }
}
