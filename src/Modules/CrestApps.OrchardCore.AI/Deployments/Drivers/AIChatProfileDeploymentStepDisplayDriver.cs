using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIChatProfileDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIChatProfileDeploymentStep>
{
    private readonly IAIChatProfileStore _openAIChatProfileStore;

    internal readonly IStringLocalizer S;

    public AIChatProfileDeploymentStepDisplayDriver(
        IAIChatProfileStore openAIChatProfileStore,
        IStringLocalizer<AIChatProfileDeploymentStepDisplayDriver> stringLocalizer)
    {
        _openAIChatProfileStore = openAIChatProfileStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIChatProfileDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AIChatProfileDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("AIChatProfileDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AIChatProfileDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIChatProfileDeploymentStepViewModel>("AIChatProfileDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.ProfileNames = step.ProfileNames;
            model.AllProfileNames = (await _openAIChatProfileStore.GetAllAsync()).Select(x => x.DisplayText).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfileDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIChatProfileDeploymentStepViewModel();

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
