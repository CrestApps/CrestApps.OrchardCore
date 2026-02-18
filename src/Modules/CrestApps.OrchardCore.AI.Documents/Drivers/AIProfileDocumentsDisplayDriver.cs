using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileDocumentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public AIProfileDocumentsDisplayDriver(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IStringLocalizer<AIProfileDocumentsDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileDocumentsViewModel>("AIProfileDocuments_Edit", async model =>
        {
            model.ProfileId = profile.ItemId;

            var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();
            model.Documents = documentsMetadata.Documents ?? [];
            model.TopN = documentsMetadata.DocumentTopN ?? 3;

            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.IndexProfileName = settings.IndexProfileName;
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);

            if (model.HasIndexProfile)
            {
                var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);
                if (indexProfile != null)
                {
                    var searchService = _serviceProvider.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);
                    model.HasVectorSearchService = searchService != null;
                }
            }
        }).Location("Content:5#Documents:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();
        documentsMetadata.DocumentTopN = model.TopN > 0 ? model.TopN : 3;
        profile.Put(documentsMetadata);

        return Edit(profile, context);
    }
}
