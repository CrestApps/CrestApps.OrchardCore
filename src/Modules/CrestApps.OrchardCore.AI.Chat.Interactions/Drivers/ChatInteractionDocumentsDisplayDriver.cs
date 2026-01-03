using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for document uploads in chat interactions.
/// Shows for all providers to enable RAG (Retrieval Augmented Generation) functionality.
/// Documents are embedded and indexed, then searched during chat to provide context.
/// </summary>
internal sealed class ChatInteractionDocumentsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public ChatInteractionDocumentsDisplayDriver(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IStringLocalizer<ChatInteractionDocumentsDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // Show documents tab for all providers - documents are embedded and used for RAG
        return Initialize<EditChatInteractionDocumentsViewModel>("ChatInteractionDocuments_Edit", async model =>
        {
            model.ItemId = interaction.ItemId;
            model.Documents = interaction.Documents ?? [];
            model.TopN = interaction.DocumentTopN ?? 3;

            // Check if index profile is configured
            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.IndexProfileName = settings.IndexProfileName;
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);

            if (model.HasIndexProfile)
            {
                // Check if the index profile has a valid embedding search service
                var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);
                if (indexProfile != null)
                {
                    // Check if there's a keyed service registered for this provider
                    var searchService = _serviceProvider.GetKeyedService<IEmbeddingSearchService>(indexProfile.ProviderName);
                    model.HasEmbeddingSearchService = searchService != null;
                }
            }
        }).Location("Parameters:3#Documents:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new EditChatInteractionDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        interaction.DocumentTopN = model.TopN > 0 ? model.TopN : 3;

        // Documents are uploaded via minimal API endpoints, so we just return the current view
        // The actual document handling happens in UploadDocumentEndpoint and RemoveDocumentEndpoint
        return Edit(interaction, context);
    }
}
