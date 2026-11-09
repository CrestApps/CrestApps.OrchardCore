using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using OrchardCore.AzureAI;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;

/// <summary>
/// Handles events for AI document azure AI search document index.
/// </summary>
public sealed class AIDocumentAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
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
                indexProfile.ProviderName != AzureAISearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.ChunkId, chunk.ChunkId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.DocumentId, chunk.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.Content, chunk.Content, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.FileName, chunk.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.ReferenceId, chunk.ReferenceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.ReferenceType, chunk.ReferenceType, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.ChunkIndex, chunk.ChunkIndex, DocumentIndexOptions.Store);

        context.DocumentIndex.Set(DocumentIndexConstants.ColumnNames.Embedding, chunk.Embedding, chunk.Embedding?.Length ?? 0, DocumentIndexOptions.Store, new Dictionary<string, object>
        {
            ["VectorSearchConfiguration"] = "default",
        });

        return Task.CompletedTask;
    }
}
