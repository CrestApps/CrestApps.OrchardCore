using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// The locations kept in Settings › Commerce › Inventory, which are also where orders can be fulfilled from.
/// </summary>
public sealed class InventoryLocationService : IInventoryLocationService, IFulfillmentLocationProvider
{
    private readonly ISiteService _siteService;

    public InventoryLocationService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<IReadOnlyList<InventoryLocation>> GetLocationsAsync()
        => (await _siteService.GetSettingsAsync<InventorySettings>()).GetLocations();

    async Task<IReadOnlyList<FulfillmentLocation>> IFulfillmentLocationProvider.GetLocationsAsync()
        => (await GetLocationsAsync()).Select(location => new FulfillmentLocation(location.LocationId, location.Name)).ToArray();
}
