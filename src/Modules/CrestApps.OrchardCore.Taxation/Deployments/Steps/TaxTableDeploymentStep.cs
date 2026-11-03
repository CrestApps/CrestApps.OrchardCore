using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax tables.
/// </summary>
public sealed class TaxTableDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxTableDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxTableDeploymentStep>("Tax Tables");

    /// <inheritdoc />
    public override string Name => TaxationDeploymentSteps.TaxTable;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
