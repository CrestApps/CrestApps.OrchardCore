using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.AzureAI;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;

public sealed class AIDocumentAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not AIDocumentChunk chunk)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            profile is not IndexProfile indexProfile ||
            indexProfile.ProviderName != AzureAISearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(AIConstants.ColumnNames.ChunkId, chunk.ChunkId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.DocumentId, chunk.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.Content, chunk.Content, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.FileName, chunk.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ReferenceId, chunk.ReferenceId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ReferenceType, chunk.ReferenceType, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.ChunkIndex, chunk.ChunkIndex, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(AIConstants.ColumnNames.Embedding, chunk.Embedding, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
