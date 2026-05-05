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

internal sealed class DeleteAIDeploymentDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, DeleteAIDeploymentDeploymentStep>
{
    private readonly INamedSourceCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIDeploymentDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentsCatalog">The deployments catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DeleteAIDeploymentDeploymentStepDisplayDriver(
        INamedSourceCatalog<AIDeployment> deploymentsCatalog,
        IStringLocalizer<DeleteAIDeploymentDeploymentStepDisplayDriver> stringLocalizer)
    {
        _deploymentsCatalog = deploymentsCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(DeleteAIDeploymentDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("DeleteAIDeploymentDeploymentStep_Summary", step).Location("Summary", "Content"),
        View("DeleteAIDeploymentDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(DeleteAIDeploymentDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIDeploymentStepViewModel>("DeleteAIDeploymentDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.DeploymentNames = step.DeploymentNames;
            model.AllDeploymentName = (await _deploymentsCatalog.GetAllAsync()).Select(d => d.Name).Order().ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(DeleteAIDeploymentDeploymentStep step, UpdateEditorContext context)
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
