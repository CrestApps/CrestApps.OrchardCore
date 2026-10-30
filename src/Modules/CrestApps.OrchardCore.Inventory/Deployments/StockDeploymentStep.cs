using CrestApps.OrchardCore.Inventory.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Inventory.Deployments;

/// <summary>
/// Exports how many units of each product and variant are on hand.
/// </summary>
public sealed class StockDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<StockDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<StockDeploymentStep>("Stock");

    /// <inheritdoc />
    public override string Name => InventoryConstants.StockRecipeStep;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
