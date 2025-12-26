using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for document uploads in chat interactions.
/// Only shows for AzureOpenAIOwnData source to enable "chat against own data" functionality.
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
        // Only show documents tab for AzureOpenAIOwnData source
        if (!string.Equals(interaction.Source, AzureOpenAIConstants.AzureOpenAIOwnData, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditChatInteractionDocumentsViewModel>("ChatInteractionDocuments_Edit", model =>
        {
            model.ItemId = interaction.ItemId;
            model.Documents = interaction.Documents ?? [];
        }).Location("Parameters:3#Documents;mb-0:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        // Only process for AzureOpenAIOwnData source
        if (!string.Equals(interaction.Source, AzureOpenAIConstants.AzureOpenAIOwnData, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Documents are uploaded via SignalR, so we just return the current view
        // The actual document handling happens in ChatInteractionHub
        return Edit(interaction, context);
    }
}
