using CrestApps.OrchardCore.Telephony.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telephony.Deployments.Drivers;

internal sealed class TelephonyExtensionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TelephonyExtensionDeploymentStep>
{
    public override IDisplayResult Display(TelephonyExtensionDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TelephonyExtensionDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TelephonyExtensionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
