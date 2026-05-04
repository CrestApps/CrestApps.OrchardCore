using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Elasticsearch;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;

/// <summary>
/// Handles events for AI document elasticsearch document index.
/// </summary>
public sealed class AIDocumentElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    /// <summary>
    /// Builds the index async.
    /// </summary>
    /// <param name="context">The context.</param>
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not AIDocumentChunkContext chunk)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            profile is not IndexProfile indexProfile ||
                indexProfile.ProviderName != ElasticsearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(AIConstants.ColumnNames.ChunkId, chunk.ChunkId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.DocumentId, chunk.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.FileName, chunk.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ReferenceId, chunk.ReferenceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ReferenceType, chunk.ReferenceType, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ChunkIndex, chunk.ChunkIndex, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.Embedding, chunk.Embedding, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.Content, chunk.Content, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
