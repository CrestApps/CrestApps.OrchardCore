using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<ContactCenterBusinessHoursCalendarDeploymentStep>("Contact Center");
    }
}
