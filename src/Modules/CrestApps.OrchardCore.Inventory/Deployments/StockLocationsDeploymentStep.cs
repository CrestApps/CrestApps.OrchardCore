using CrestApps.OrchardCore.Inventory.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Inventory.Deployments;

/// <summary>
/// Exports the store's own stock locations, with their ids, as a <c>StockLocations</c> recipe step. Locations another module owns, such as a technician's van, are left out.
/// </summary>
public sealed class StockLocationsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<StockLocationsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<StockLocationsDeploymentStep>("Stock locations");

    /// <inheritdoc/>
    public override string Name => InventoryConstants.StockLocationsRecipeStep;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
