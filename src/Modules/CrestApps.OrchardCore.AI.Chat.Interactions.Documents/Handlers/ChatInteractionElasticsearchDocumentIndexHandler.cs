using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;

public sealed class ChatInteractionElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ChatInteractionDocument chatInteractionDocument)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue("Interaction", out var v) ||
            v is not ChatInteraction interaction ||
            interaction.Source != ElasticsearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        if (context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) && profile is IndexProfile indexProfile)
        {
            var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

            if (metadata.StoreSourceData)
            {
                context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Text, chatInteractionDocument.Text, DocumentIndexOptions.Store);
            }
        }

        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.DocumentId, chatInteractionDocument.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.InteractionId, interaction.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.FileName, chatInteractionDocument.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Chunks, chatInteractionDocument.Chunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
