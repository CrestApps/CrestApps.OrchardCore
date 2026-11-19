using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Agreements.Deployments;

/// <summary>
/// Exports every agreement.
/// </summary>
public sealed class AgreementsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AgreementsDeploymentStep>("Agreements");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AgreementsDeploymentStep>("Agreements");

    /// <inheritdoc/>
    public override string Name => AgreementsConstants.RecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
