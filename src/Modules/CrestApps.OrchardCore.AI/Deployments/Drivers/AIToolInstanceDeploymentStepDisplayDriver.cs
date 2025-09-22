using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIToolInstanceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIToolInstanceDeploymentStep>
{
    private readonly ICatalog<AIToolInstance> _store;

    internal readonly IStringLocalizer S;

    public AIToolInstanceDeploymentStepDisplayDriver(
        ICatalog<AIToolInstance> store,
        IStringLocalizer<AIToolInstanceDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIToolInstanceDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AIToolInstanceDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("AIToolInstanceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AIToolInstanceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIToolInstanceDeploymentStepViewModel>("AIToolInstanceDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Instances = (await _store.GetAllAsync()).Select(x => new AIToolInstanceEntryViewModel
            {
                ItemId = x.ItemId,
                DisplayText = x.DisplayText,
                IsSelected = step.InstanceIds?.Contains(x.ItemId) ?? false
            }).OrderBy(x => x.DisplayText)
            .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstanceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIToolInstanceDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Instances);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.InstanceIds = [];
        }
        else
        {
            if (model.Instances == null || model.Instances.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instances), S["At least one instance is required."]);
            }

            step.IncludeAll = false;
            step.InstanceIds = model.Instances.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();
        }

        return Edit(step, context);
    }
}
