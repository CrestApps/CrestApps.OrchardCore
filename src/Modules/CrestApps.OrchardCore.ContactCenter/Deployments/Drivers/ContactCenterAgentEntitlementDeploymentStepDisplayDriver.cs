using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterAgentEntitlementDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterAgentEntitlementDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterAgentEntitlementDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterAgentEntitlementDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterAgentEntitlementDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
