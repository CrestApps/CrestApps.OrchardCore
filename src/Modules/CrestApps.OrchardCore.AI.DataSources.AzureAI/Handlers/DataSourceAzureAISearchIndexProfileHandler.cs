using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;

public sealed class DataSourceAzureAISearchIndexProfileHandler : DataSourceIndexProfileHandlerBase
{
    public DataSourceAzureAISearchIndexProfileHandler(IAIClientFactory aiClientFactory)
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
        var profileMetadata = indexProfile.As<DataSourceIndexProfileMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(profileMetadata);

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.ReferenceId,
            Type = DocumentIndex.Types.Text,
            IsKey = true,
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
            AzureFieldKey = DataSourceConstants.ColumnNames.Title,
            Type = DocumentIndex.Types.Text,
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap
        {
            AzureFieldKey = DataSourceConstants.ColumnNames.Text,
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
            AzureFieldKey = DataSourceConstants.ColumnNames.Chunks,
            Type = DocumentIndex.Types.Complex,
            SubFields =
            [
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = DataSourceConstants.ColumnNames.ChunksColumnNames.Text,
                    Type = DocumentIndex.Types.Text,
                    IsSearchable = true,
                },
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = DataSourceConstants.ColumnNames.ChunksColumnNames.Embedding,
                    Type = DocumentIndex.Types.Number,
                    VectorInfo = new AzureAISearchIndexMapVectorInfo
                    {
                        Dimensions = embeddingDimensions,
                    },
                },
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = DataSourceConstants.ColumnNames.ChunksColumnNames.Index,
                    Type = DocumentIndex.Types.Integer,
                },
            ]
        });

        indexProfile.Put(metadata);
    }
}
