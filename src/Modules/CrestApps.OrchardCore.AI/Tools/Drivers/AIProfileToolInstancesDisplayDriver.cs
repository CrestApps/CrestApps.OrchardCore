using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIProfileToolInstancesDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IModelStore<AIToolInstance> _toolInstanceStore;

    internal readonly IStringLocalizer S;

    public AIProfileToolInstancesDisplayDriver(
        IModelStore<AIToolInstance> toolInstanceStore,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolInstanceStore = toolInstanceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileToolInstancesViewModel>("EditProfileToolInstances_Edit", async model =>
        {
            var toolMetadata = profile.As<AIProfileFunctionInstancesMetadata>();

            var instances = await _toolInstanceStore.GetAllAsync();

            model.Instances = instances
            .Select(instance => new ToolEntry
            {
                Id = instance.Id,
                DisplayText = instance.DisplayText,
                Description = instance.As<InvokableToolMetadata>()?.Description,
                IsSelected = toolMetadata.InstanceIds?.Contains(instance.Id) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Content:8Content:8.5#Capabilities:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = new AIProfileFunctionInstancesMetadata
        {
            InstanceIds = model.Instances?.Where(x => x.IsSelected).Select(x => x.Id).ToArray() ?? []
        };

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
