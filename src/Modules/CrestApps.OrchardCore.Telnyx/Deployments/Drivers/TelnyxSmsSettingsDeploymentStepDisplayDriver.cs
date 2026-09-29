using CrestApps.OrchardCore.Telnyx.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Drivers;

internal sealed class TelnyxSmsSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TelnyxSmsSettingsDeploymentStep>
{
    public override IDisplayResult Display(TelnyxSmsSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TelnyxSmsSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TelnyxSmsSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
