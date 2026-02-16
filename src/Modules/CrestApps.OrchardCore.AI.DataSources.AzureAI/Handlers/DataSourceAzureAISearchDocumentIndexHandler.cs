using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.AzureAI;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;

public sealed class DataSourceAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not DataSourceEmbeddingDocument embeddingDocument)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            profile is not IndexProfile indexProfile ||
            indexProfile.ProviderName != AzureAISearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.ReferenceId, embeddingDocument.ReferenceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.DataSourceId, embeddingDocument.DataSourceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Title, embeddingDocument.Title, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Text, embeddingDocument.Text, DocumentIndexOptions.Store);

        if (embeddingDocument.Timestamp.HasValue)
        {
            context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Timestamp, embeddingDocument.Timestamp.Value, DocumentIndexOptions.Store);
        }

        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Chunks, embeddingDocument.Chunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
