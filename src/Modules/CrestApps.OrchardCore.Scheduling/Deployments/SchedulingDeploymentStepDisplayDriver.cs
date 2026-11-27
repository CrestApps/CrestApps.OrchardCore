using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Scheduling.Deployments;

internal sealed class SchedulingDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SchedulingDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public SchedulingDeploymentStepDisplayDriver(IStringLocalizer<SchedulingDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

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
        }).Location(l => l.Zone("Content", "1").Card(S["Scheduling"], "10"));
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
