using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Tools.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

public sealed class AIProfileToolMetadataDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly INamedCatalog<AIProfile> _profileStore;

    internal readonly IStringLocalizer S;

    public AIProfileToolMetadataDisplayDriver(
        INamedCatalog<AIProfile> profileStore,
        IStringLocalizer<AIProfileToolMetadataDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (instance.Source != ProfileAwareAIToolSource.ToolSource)
        {
            return null;
        }

        return Initialize<AIProfileFunctionMetadataViewModel>("AIProfileFunctionMetadata_Edit", async model =>
        {
            var metadata = instance.As<AIProfileFunctionMetadata>();

            model.ProfileId = metadata.ProfileId;

            model.Profiles = (await _profileStore.GetAllAsync())
            .Select(profile => new SelectListItem(profile.DisplayText, profile.Id));
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (instance.Source != ProfileAwareAIToolSource.ToolSource)
        {
            return null;
        }

        var model = new AIProfileFunctionMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(nameof(model.ProfileId), S["The Profile field is required."]);
        }
        else if (await _profileStore.FindByIdAsync(model.ProfileId) == null)
        {
            context.Updater.ModelState.AddModelError(nameof(model.ProfileId), S["The selected Profile does not exist."]);
        }

        instance.Put(new AIProfileFunctionMetadata
        {
            ProfileId = model.ProfileId
        });

        return Edit(instance, context);
    }
}
