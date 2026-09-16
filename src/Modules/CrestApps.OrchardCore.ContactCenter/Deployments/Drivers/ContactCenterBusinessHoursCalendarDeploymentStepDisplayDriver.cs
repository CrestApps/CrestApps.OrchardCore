using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;

internal sealed class ContactCenterBusinessHoursCalendarDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ContactCenterBusinessHoursCalendarDeploymentStep>
{
    public override IDisplayResult Display(ContactCenterBusinessHoursCalendarDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ContactCenterBusinessHoursCalendarDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ContactCenterBusinessHoursCalendarDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
