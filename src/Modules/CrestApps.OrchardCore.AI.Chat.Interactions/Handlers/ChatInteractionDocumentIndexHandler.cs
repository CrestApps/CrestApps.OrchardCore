using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

public sealed class ChatInteractionDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ChatInteractionDocument chatInteractionDocument)
        {
            return Task.CompletedTask;
        }

        if (!context.AdditionalProperties.TryGetValue("Interaction", out var v) ||
            v is not ChatInteraction interaction)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set("documentId", chatInteractionDocument.DocumentId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set("InteractionId", interaction.ItemId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set("fileName", chatInteractionDocument.FileName, DocumentIndexOptions.Store);
        context.DocumentIndex.Set("chunks", chatInteractionDocument.ContentChunks, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
