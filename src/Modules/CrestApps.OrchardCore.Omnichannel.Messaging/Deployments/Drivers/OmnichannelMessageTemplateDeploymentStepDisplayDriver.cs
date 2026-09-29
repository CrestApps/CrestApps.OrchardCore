using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Drivers;

internal sealed class OmnichannelMessageTemplateDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelMessageTemplateDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelMessageTemplateDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelMessageTemplateDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelMessageTemplateDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
