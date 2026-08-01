using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelDispositionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelDispositionDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelDispositionDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelDispositionDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelDispositionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
