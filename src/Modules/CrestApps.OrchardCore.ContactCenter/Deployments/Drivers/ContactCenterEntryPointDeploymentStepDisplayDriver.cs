using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterEntryPointDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterEntryPointDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterEntryPointDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterEntryPointDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterEntryPointDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
