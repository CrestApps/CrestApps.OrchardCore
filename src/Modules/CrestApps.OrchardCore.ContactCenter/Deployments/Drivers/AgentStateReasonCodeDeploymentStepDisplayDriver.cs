using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class AgentStateReasonCodeDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AgentStateReasonCodeDeploymentStep>
{
    public override IDisplayResult Display(AgentStateReasonCodeDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("AgentStateReasonCodeDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AgentStateReasonCodeDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
