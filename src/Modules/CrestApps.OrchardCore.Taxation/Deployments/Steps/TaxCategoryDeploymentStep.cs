using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax categories.
/// </summary>
public sealed class TaxCategoryDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxCategoryDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxCategoryDeploymentStep>("Tax Categories");

    /// <inheritdoc />
    public override string Name => TaxationDeploymentSteps.TaxCategory;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
