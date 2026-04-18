using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Core.Handlers;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;

internal sealed class DataSourceAzureAISearchIndexProfileHandler : DataSourceIndexProfileHandlerBase
{
    public DataSourceAzureAISearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    : base(AzureAISearchConstants.ProviderName, deploymentManager, aiClientFactory)
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

        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.ChunkId,
            Type = DocumentIndex.Types.Text,
            IsKey = true,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.ReferenceId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.DataSourceId,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.ReferenceType,
            Type = DocumentIndex.Types.Text,
            IsFilterable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.ChunkIndex,
            Type = DocumentIndex.Types.Integer,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.Title,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.Content,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.Timestamp,
            Type = DocumentIndex.Types.DateTime,
            IsFilterable = true,
            IsSortable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.Embedding,
            Type = DocumentIndex.Types.Number,
            VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = embeddingDimensions,
            },
        });

        indexProfile.Put(metadata);
    }
}
