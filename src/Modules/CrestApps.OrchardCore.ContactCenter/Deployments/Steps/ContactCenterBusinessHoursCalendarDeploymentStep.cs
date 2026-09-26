using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center business-hours calendars.
/// </summary>
public sealed class ContactCenterBusinessHoursCalendarDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarDeploymentStep"/> class.
    /// </summary>
    public ContactCenterBusinessHoursCalendarDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.BusinessHoursCalendar;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterBusinessHoursCalendarDeploymentStep(IStringLocalizer<ContactCenterBusinessHoursCalendarDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
