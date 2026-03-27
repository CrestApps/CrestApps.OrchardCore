using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileSessionDocumentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;

    public AIProfileSessionDocumentsDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionDocumentsViewModel>("AIProfileSessionDocuments_Edit", async model =>
        {
            var metadata = profile.As<AIProfileSessionDocumentsMetadata>();
            model.AllowSessionDocuments = metadata.AllowSessionDocuments;

            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);
        }).Location("Content:5#Documents:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileSessionDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = profile.As<AIProfileSessionDocumentsMetadata>();
        metadata.AllowSessionDocuments = model.AllowSessionDocuments;
        profile.Put(metadata);

        return Edit(profile, context);
    }
}
