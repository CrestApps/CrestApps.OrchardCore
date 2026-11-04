using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>BusinessHoursCalendar</c> recipe step.
/// </summary>
internal sealed class ContactCenterBusinessHoursCalendarDeploymentSource : CatalogDeploymentSource<ContactCenterBusinessHoursCalendarDeploymentStep, BusinessHoursCalendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the business-hours calendars.</param>
    public ContactCenterBusinessHoursCalendarDeploymentSource(IBusinessHoursCalendarManager manager)
        : base("Calendars", manager)
    {
    }
}
