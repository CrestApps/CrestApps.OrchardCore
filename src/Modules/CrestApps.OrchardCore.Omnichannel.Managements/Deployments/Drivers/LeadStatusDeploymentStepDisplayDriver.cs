using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class LeadStatusDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LeadStatusDeploymentStep>
{
    public override IDisplayResult Display(LeadStatusDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("LeadStatusDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LeadStatusDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
