using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;

public sealed class ChatInteractionAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not AIDocument document)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue("Interaction", out var v) ||
            v is not ChatInteraction interaction ||
            interaction.Source != AzureAISearchConstants.ProviderName)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Text, document.Text, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.DocumentId, document.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.InteractionId, interaction.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.FileName, document.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(ChatInteractionsConstants.ColumnNames.Chunks, document.Chunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
