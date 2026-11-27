using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Deployments;

internal sealed class WorkforceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkforceDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public WorkforceDeploymentStepDisplayDriver(IStringLocalizer<WorkforceDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(WorkforceDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("WorkforceDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkforceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(WorkforceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<WorkforceDeploymentStepViewModel>("WorkforceDeploymentStep_Fields_Edit", model => model.IncludeWorkers = step.IncludeWorkers)
            .Location(l => l.Zone("Content").Card(S["Workforce Teams And Workers"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkforceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new WorkforceDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeWorkers = model.IncludeWorkers;

        return Edit(step, context);
    }
}
