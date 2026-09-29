using CrestApps.OrchardCore.Telnyx.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Drivers;

internal sealed class TelnyxSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TelnyxSettingsDeploymentStep>
{
    public override IDisplayResult Display(TelnyxSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TelnyxSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TelnyxSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
