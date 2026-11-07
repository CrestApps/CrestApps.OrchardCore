using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center business-hours calendars carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class ContactCenterBusinessHoursCalendarStep : CatalogRecipeStep<BusinessHoursCalendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the business-hours calendars.</param>
    public ContactCenterBusinessHoursCalendarStep(IBusinessHoursCalendarManager manager)
        : base(ContactCenterDeploymentSteps.BusinessHoursCalendar, "Calendars", manager)
    {
    }
}
