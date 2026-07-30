using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterQueueGroupDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterQueueGroupDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterQueueGroupDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterQueueGroupDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterQueueGroupDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
