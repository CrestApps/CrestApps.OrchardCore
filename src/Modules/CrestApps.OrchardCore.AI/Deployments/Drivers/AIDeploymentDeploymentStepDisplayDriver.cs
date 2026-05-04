using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIDeploymentDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIDeploymentDeploymentStep>
{
    private readonly INamedSourceCatalog<AIDeployment> _deploymentCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The deployment catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDeploymentDeploymentStepDisplayDriver(
        INamedSourceCatalog<AIDeployment> deploymentCatalog,
        IStringLocalizer<AIProfileDeploymentStepDisplayDriver> stringLocalizer)
    {
        _deploymentCatalog = deploymentCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIDeploymentDeploymentStep step, BuildDisplayContext context)
    {
        return

        CombineAsync(
            View("AIDeploymentDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AIDeploymentDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(AIDeploymentDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIDeploymentStepViewModel>("AIDeploymentDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.DeploymentNames = step.DeploymentNames;
            model.AllDeploymentName = (await _deploymentCatalog.GetAllAsync()).Select(d => d.Name).Order().ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeploymentDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
        p => p.IncludeAll,
        p => p.DeploymentNames);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.DeploymentNames = [];
        }
        else
        {
            if (model.DeploymentNames == null || model.DeploymentNames.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DeploymentNames), S["At least one deployment name is required."]);
            }

            step.IncludeAll = false;
            step.DeploymentNames = model.DeploymentNames;
        }

        return Edit(step, context);
    }
}
