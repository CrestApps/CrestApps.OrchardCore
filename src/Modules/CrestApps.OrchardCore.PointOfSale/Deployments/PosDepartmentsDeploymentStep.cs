using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Exports every department registers ring amounts up in, with its number, tax category, key color and limits, as a
/// <c>PosDepartments</c> recipe step. Add it before the registers step, whose keys name departments.
/// </summary>
public sealed class PosDepartmentsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PosDepartmentsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PosDepartmentsDeploymentStep>("Register departments");

    /// <inheritdoc/>
    public override string Name => PosConstants.Recipes.Departments;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
