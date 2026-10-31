using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Deployments;

internal sealed class WorkforceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkforceDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(WorkforceDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("WorkforceDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkforceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(WorkforceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<WorkforceDeploymentStepViewModel>("WorkforceDeploymentStep_Fields_Edit", model => model.IncludeWorkers = step.IncludeWorkers).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkforceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new WorkforceDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeWorkers = model.IncludeWorkers;

        return Edit(step, context);
    }
}
