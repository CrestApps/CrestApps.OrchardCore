using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIProfileDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIProfileDeploymentStep>
{
    private readonly IAIProfileStore _store;

    internal readonly IStringLocalizer S;

    public AIProfileDeploymentStepDisplayDriver(
        IAIProfileStore store,
        IStringLocalizer<AIProfileDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfileDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AIProfileDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("AIProfileDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AIProfileDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIProfileDeploymentStepViewModel>("AIProfileDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.ProfileNames = step.ProfileNames;
            model.AllProfileNames = (await _store.GetAllAsync()).Select(x => x.DisplayText ?? x.Name).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIProfileDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.ProfileNames);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.ProfileNames = [];
        }
        else
        {
            if (model.ProfileNames == null || model.ProfileNames.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileNames), S["At least one profile name is required."]);
            }

            step.IncludeAll = false;
            step.ProfileNames = model.ProfileNames;
        }

        return Edit(step, context);
    }
}
