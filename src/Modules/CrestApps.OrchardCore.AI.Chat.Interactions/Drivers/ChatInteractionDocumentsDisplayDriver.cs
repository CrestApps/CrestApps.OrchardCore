using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for document uploads in chat interactions.
/// Shows for all providers to enable RAG (Retrieval Augmented Generation) functionality.
/// Documents are embedded and indexed, then searched during chat to provide context.
/// </summary>
internal sealed class ChatInteractionDocumentsDisplayDriver : DisplayDriver<ChatInteraction>
{
    internal readonly IStringLocalizer S;

    public ChatInteractionDocumentsDisplayDriver(
        IStringLocalizer<ChatInteractionDocumentsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // Show documents tab for all providers - documents are embedded and used for RAG
        return Initialize<EditChatInteractionDocumentsViewModel>("ChatInteractionDocuments_Edit", model =>
        {
            model.ItemId = interaction.ItemId;
            model.Documents = interaction.Documents ?? [];
        }).Location("Parameters:3#Documents:3");
    }

    public override Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        // Documents are uploaded via minimal API endpoints, so we just return the current view
        // The actual document handling happens in UploadDocumentEndpoint and RemoveDocumentEndpoint
        return Task.FromResult(Edit(interaction, context));
    }
}
