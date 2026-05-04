using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.AI.Memory.Models;
using OrchardCore.Elasticsearch;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Elasticsearch.Handlers;

/// <summary>
/// Handles events for AI memory elasticsearch document index.
/// </summary>
public sealed class AIMemoryElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    /// <summary>
    /// Builds the index async.
    /// </summary>
    /// <param name="context">The context.</param>
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not AIMemoryEntryIndexDocument memory)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            profile is not IndexProfile indexProfile ||
                !string.Equals(indexProfile.ProviderName, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(MemoryConstants.ColumnNames.MemoryId, memory.MemoryId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.UserId, memory.UserId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.Name, memory.Name, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.Description, memory.Description, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.Content, memory.Content, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.UpdatedUtc, memory.UpdatedUtc, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(MemoryConstants.ColumnNames.Embedding, memory.Embedding, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
