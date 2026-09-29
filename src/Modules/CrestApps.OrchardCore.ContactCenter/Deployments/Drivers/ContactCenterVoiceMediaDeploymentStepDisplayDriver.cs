using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterVoiceMediaDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterVoiceMediaDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterVoiceMediaDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterVoiceMediaDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterVoiceMediaDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
