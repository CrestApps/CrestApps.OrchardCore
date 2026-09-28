using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterSkillDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterSkillDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterSkillDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterSkillDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterSkillDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
