using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.BusinessHours.Deployments;

internal sealed class BusinessHoursSchedulesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, BusinessHoursSchedulesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public BusinessHoursSchedulesDeploymentStepDisplayDriver(IStringLocalizer<BusinessHoursSchedulesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(BusinessHoursSchedulesDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("BusinessHoursSchedulesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("BusinessHoursSchedulesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(BusinessHoursSchedulesDeploymentStep step, BuildEditorContext context)
        => View("BusinessHoursSchedulesDeploymentStep_Fields_Edit", step)
            .Location(l => l.Zone("Content", "1").Card(S["Business Hours Schedules"], "10"));
}
