using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;

internal sealed class DataSourceElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not DataSourceEmbeddingDocument embeddingDocument)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            profile is not IndexProfile indexProfile ||
            indexProfile.ProviderName != ElasticsearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

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
