using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;

public sealed class ChatInteractionAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ChatInteractionDocument chatInteractionDocument)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue("Interaction", out var v) ||
            v is not ChatInteraction interaction ||
            interaction.Source != AzureAISearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Text, chatInteractionDocument.Text, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.DocumentId, chatInteractionDocument.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.InteractionId, interaction.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.FileName, chatInteractionDocument.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Chunks, chatInteractionDocument.Chunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
