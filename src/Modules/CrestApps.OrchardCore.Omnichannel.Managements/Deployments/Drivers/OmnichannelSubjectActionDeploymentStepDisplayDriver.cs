using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelSubjectActionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelSubjectActionDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelSubjectActionDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelSubjectActionDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelSubjectActionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
