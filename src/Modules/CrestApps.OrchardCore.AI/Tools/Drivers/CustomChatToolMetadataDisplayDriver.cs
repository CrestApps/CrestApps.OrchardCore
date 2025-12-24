using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Tools.Models;
using CrestApps.OrchardCore.AI.Tools.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class CustomChatToolMetadataDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly IStringLocalizer S;

    public CustomChatToolMetadataDisplayDriver(
        IStringLocalizer<CustomChatToolMetadataDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (instance.Source != CustomChatToolSource.ToolSource)
        {
            return null;
        }

        return Initialize<CustomChatToolViewModel>("CustomChatToolMetadata_Edit", model =>
        {
            var metadata = instance.As<CustomChatToolMetadata>();

            model.CustomChatInstanceId = metadata.CustomChatInstanceId;
            model.Instances = [];
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (instance.Source != CustomChatToolSource.ToolSource)
        {
            return null;
        }

        var model = new CustomChatToolViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.CustomChatInstanceId))
        {
            context.Updater.ModelState.AddModelError(nameof(model.CustomChatInstanceId), S["Custom Chat Instance is required."]);
        }

        instance.Put(new CustomChatToolMetadata
        {
            CustomChatInstanceId = model.CustomChatInstanceId
        });

        return Edit(instance, context);
    }
}
