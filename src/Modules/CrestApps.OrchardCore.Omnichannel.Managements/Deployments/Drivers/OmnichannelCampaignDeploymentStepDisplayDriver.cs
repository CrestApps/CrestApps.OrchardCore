using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelCampaignDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelCampaignDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelCampaignDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelCampaignDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelCampaignDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
