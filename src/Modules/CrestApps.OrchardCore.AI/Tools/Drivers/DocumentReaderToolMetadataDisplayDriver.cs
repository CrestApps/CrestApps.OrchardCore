using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Tools.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class DocumentReaderToolMetadataDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly IStringLocalizer S;

    public DocumentReaderToolMetadataDisplayDriver(IStringLocalizer<DocumentReaderToolMetadataDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (instance.Source != DocumentReaderToolSource.ToolSource)
        {
            return null;
        }

        return Initialize<DocumentReaderToolMetadata>("DocumentReaderToolMetadata_Edit", model =>
            {
                var metadata = instance.As<DocumentReaderToolMetadata>();

                model.MaxWords = metadata?.MaxWords ?? 200;

            }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (instance.Source != DocumentReaderToolSource.ToolSource)
        {
            return null;
        }

        var model = new DocumentReaderToolMetadata();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxWords <= 0)
        {
            context.Updater.ModelState.AddModelError(nameof(model.MaxWords), S["Max words must be greater than zero."]);
        }

        instance.Put(model);

        return Edit(instance, context);
    }
}
