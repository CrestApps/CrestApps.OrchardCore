using CrestApps.OrchardCore.AI.Core;
using CrestApps.AI;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileTemplateSessionDocumentsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionDocumentsViewModel>("AIProfileSessionDocuments_Edit", model =>
        {
            var metadata = template.As<AIProfileSessionDocumentsMetadata>();
            model.AllowSessionDocuments = metadata.AllowSessionDocuments;
            model.HasIndexProfile = true;
        }).Location("Content:5#Documents:10")
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

        var metadata = template.As<AIProfileSessionDocumentsMetadata>();
        metadata.AllowSessionDocuments = model.AllowSessionDocuments;
        template.Put(metadata);

        return Edit(template, context);
    }
}
