using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelChannelEndpointDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelChannelEndpointDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelChannelEndpointDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelChannelEndpointDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelChannelEndpointDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
