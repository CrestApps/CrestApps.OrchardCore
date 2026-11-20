using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.BusinessHours.Deployments;

/// <summary>
/// Exports every business hours schedule.
/// </summary>
public sealed class BusinessHoursSchedulesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<BusinessHoursSchedulesDeploymentStep>("Configuration");
    private static readonly LocalizationSource _title = LocalizationSource.Create<BusinessHoursSchedulesDeploymentStep>("Business hours schedules");

    /// <inheritdoc />
    public override string Name => BusinessHoursConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
