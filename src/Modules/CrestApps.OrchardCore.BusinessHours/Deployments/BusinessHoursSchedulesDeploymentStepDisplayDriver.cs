using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.BusinessHours.Deployments;

internal sealed class BusinessHoursSchedulesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, BusinessHoursSchedulesDeploymentStep>
{
    public override IDisplayResult Display(BusinessHoursSchedulesDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("BusinessHoursSchedulesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("BusinessHoursSchedulesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(BusinessHoursSchedulesDeploymentStep step, BuildEditorContext context)
        => View("BusinessHoursSchedulesDeploymentStep_Fields_Edit", step).Location("Content");
}
