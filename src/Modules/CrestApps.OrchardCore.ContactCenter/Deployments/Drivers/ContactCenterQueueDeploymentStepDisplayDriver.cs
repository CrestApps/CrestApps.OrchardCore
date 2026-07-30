using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterQueueDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterQueueDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterQueueDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterQueueDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterQueueDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
