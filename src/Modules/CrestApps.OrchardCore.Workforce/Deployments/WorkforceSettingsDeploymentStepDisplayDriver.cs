using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Shows the Workforce settings deployment step.
/// </summary>
internal sealed class WorkforceSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkforceSettingsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(WorkforceSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("WorkforceSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkforceSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(WorkforceSettingsDeploymentStep step, BuildEditorContext context)
    {
        return View("WorkforceSettingsDeploymentStep_Fields_Edit", step).Location("Content");
    }
}
