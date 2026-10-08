using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center business-hours calendars.
/// </summary>
public sealed class ContactCenterBusinessHoursCalendarDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterBusinessHoursCalendarDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterBusinessHoursCalendarDeploymentStep>("Contact Center Business Hours Calendars");

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarDeploymentStep"/> class.
    /// </summary>
    public ContactCenterBusinessHoursCalendarDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.BusinessHoursCalendar;
        Category = _category;
        Title = _title;
    }
}
