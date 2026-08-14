using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelCampaignGroupDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelCampaignGroupDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelCampaignGroupDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelCampaignGroupDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelCampaignGroupDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
