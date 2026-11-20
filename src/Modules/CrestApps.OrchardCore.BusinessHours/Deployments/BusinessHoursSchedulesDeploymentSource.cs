using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Recipes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.BusinessHours.Deployments;

/// <summary>
/// Exports every business hours schedule as the <c>BusinessHoursSchedules</c> recipe step.
/// </summary>
internal sealed class BusinessHoursSchedulesDeploymentSource : NamedCatalogDeploymentSource<BusinessHoursSchedulesDeploymentStep, BusinessHoursSchedule>
{
    public BusinessHoursSchedulesDeploymentSource(INamedCatalogManager<BusinessHoursSchedule> manager)
        : base(BusinessHoursSchedulesRecipeStep.EntriesName, manager)
    {
    }
}
