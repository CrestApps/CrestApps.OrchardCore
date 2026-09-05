using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class CadenceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, CadenceDeploymentStep>
{
    public override IDisplayResult Display(CadenceDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("CadenceDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("CadenceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
