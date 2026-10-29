using System.Text.Json.Serialization;
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

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.BusinessHoursCalendar;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
