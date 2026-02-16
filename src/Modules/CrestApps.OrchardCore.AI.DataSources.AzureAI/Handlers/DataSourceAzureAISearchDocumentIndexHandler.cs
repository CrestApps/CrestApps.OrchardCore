using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.AzureAI;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;

internal sealed class DataSourceAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
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

        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.ChunkId, embeddingDocument.ChunkId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.ReferenceId, embeddingDocument.ReferenceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.DataSourceId, embeddingDocument.DataSourceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.ChunkIndex, embeddingDocument.ChunkIndex, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Title, embeddingDocument.Title, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Content, embeddingDocument.Content, DocumentIndexOptions.Store);

        if (embeddingDocument.Timestamp.HasValue)
        {
            context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Timestamp, embeddingDocument.Timestamp.Value, DocumentIndexOptions.Store);
        }

        if (embeddingDocument.Embedding != null)
        {
            context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Embedding, embeddingDocument.Embedding, DocumentIndexOptions.Store);
        }

        if (embeddingDocument.Filters != null)
        {
            context.DocumentIndex.Set(DataSourceConstants.ColumnNames.Filters, embeddingDocument.Filters, DocumentIndexOptions.Store);
        }

        return Task.CompletedTask;
    }
}
