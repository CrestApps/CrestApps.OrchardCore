using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OpportunityStageDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OpportunityStageDeploymentStep>
{
    public override IDisplayResult Display(OpportunityStageDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OpportunityStageDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OpportunityStageDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
