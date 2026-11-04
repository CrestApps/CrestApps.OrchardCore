using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Recipes;

namespace CrestApps.OrchardCore.Inventory.Deployments;

/// <summary>
/// Writes the store's own stock locations into a <c>StockLocations</c> recipe step, in their order, keyed by their ids.
/// Locations another module owns, such as a technician's van, are left out: that module keeps them.
/// </summary>
internal sealed class StockLocationsDeploymentSource : CatalogStoreDeploymentSource<StockLocationsDeploymentStep, StockLocation>
{
    public StockLocationsDeploymentSource(ICatalog<StockLocation> store)
        : base(StockLocationsStep.EntriesName, store)
    {
    }

    protected override IEnumerable<StockLocation> Order(IEnumerable<StockLocation> entries)
        => entries.OrderBy(entry => entry.Position).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    protected override bool IsSelected(StockLocationsDeploymentStep step, StockLocation entry)
        => entry.OwnerKind is null;
}
