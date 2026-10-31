using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Scheduling.Deployments;

internal sealed class SchedulingDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SchedulingDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(SchedulingDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SchedulingDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SchedulingDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(SchedulingDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<SchedulingDeploymentStepViewModel>("SchedulingDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeAppointments = step.IncludeAppointments;
            model.IncludeWaitlist = step.IncludeWaitlist;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SchedulingDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SchedulingDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeAppointments = model.IncludeAppointments;
        step.IncludeWaitlist = model.IncludeWaitlist;

        return Edit(step, context);
    }
}
