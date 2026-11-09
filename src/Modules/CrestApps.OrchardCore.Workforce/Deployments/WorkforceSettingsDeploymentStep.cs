using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Exports the Workforce settings: shift templates, rotations and coverage requirements, time-off types, policies,
/// allotments and blackout dates, the time clock's pay periods, pay codes, policies, kiosks and payroll CSV columns, and
/// the labor rule sets, as the recipe steps of the features that are on.
/// </summary>
public sealed class WorkforceSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WorkforceSettingsDeploymentStep>("Workforce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WorkforceSettingsDeploymentStep>("Workforce settings");

    /// <inheritdoc />
    public override string Name => "WorkforceSettings";

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
