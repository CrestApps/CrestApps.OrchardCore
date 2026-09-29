using CrestApps.OrchardCore.Asterisk.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Asterisk.Deployments.Drivers;

internal sealed class AsteriskSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AsteriskSettingsDeploymentStep>
{
    public override IDisplayResult Display(AsteriskSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("AsteriskSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AsteriskSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
