using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileSessionDocumentsDisplayDriver : DisplayDriver<AIProfile>
{
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionDocumentsViewModel>("AIProfileSessionDocuments_Edit", model =>
        {
            var metadata = profile.As<AIProfileSessionDocumentsMetadata>();
            model.AllowSessionDocuments = metadata.AllowSessionDocuments;
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
