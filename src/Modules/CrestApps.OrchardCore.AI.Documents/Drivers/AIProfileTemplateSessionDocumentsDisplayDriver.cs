using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileTemplateSessionDocumentsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateSessionDocumentsDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public AIProfileTemplateSessionDocumentsDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionDocumentsViewModel>("AIProfileSessionDocuments_Edit", async model =>
        {
            var metadata = template.GetOrCreate<AIProfileSessionDocumentsMetadata>();
            model.AllowSessionDocuments = metadata.AllowSessionDocuments;
            model.AllowSessionImageUploads = metadata.AllowSessionImageUploads;

            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);
        }).Location("Content:2#Knowledge;2")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditAIProfileSessionDocumentsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = template.GetOrCreate<AIProfileSessionDocumentsMetadata>();
        metadata.AllowSessionDocuments = model.AllowSessionDocuments;
        metadata.AllowSessionImageUploads = model.AllowSessionImageUploads;
        template.Put(metadata);

        return Edit(template, context);
    }
}
