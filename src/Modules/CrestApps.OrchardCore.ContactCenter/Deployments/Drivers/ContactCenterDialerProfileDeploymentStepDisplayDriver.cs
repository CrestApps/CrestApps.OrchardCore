using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterDialerProfileDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterDialerProfileDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterDialerProfileDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterDialerProfileDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterDialerProfileDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
