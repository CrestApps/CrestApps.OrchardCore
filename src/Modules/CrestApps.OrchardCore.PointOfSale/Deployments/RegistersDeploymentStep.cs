using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Exports every point-of-sale register, with its store location, address, currency and roles, as a <c>Registers</c> recipe step. Shifts and sales stay on the site.
/// </summary>
public sealed class RegistersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<RegistersDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<RegistersDeploymentStep>("Registers");

    /// <inheritdoc/>
    public override string Name => PosConstants.Recipes.Registers;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
