using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch.Handlers;

public sealed class ChatInteractionElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not AIDocument document)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue("Interaction", out var v) ||
            v is not ChatInteraction interaction)
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

        if (metadata.StoreSourceData)
        {
            context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Text, document.Text, DocumentIndexOptions.Store);
        }

        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.DocumentId, document.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.InteractionId, interaction.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.FileName, document.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Chunks, document.Chunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
