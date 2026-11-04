using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Gives the stock locations, in the order orders take units from them: the site's own in the store, then those owned
/// by other records, then any still kept in the settings from before locations had a store; the default location is
/// first when it isn't stored. Orders are fulfilled only from the site's own locations, not from ones owned by another
/// record, such as a van.
/// </summary>
public sealed class InventoryLocationService : IInventoryLocationService, IFulfillmentLocationProvider
{
    /// <summary>
    /// The name of the default location when it hasn't been renamed.
    /// </summary>
    public const string DefaultLocationName = "Main location";

    private readonly ISiteService _siteService;
    private readonly StockLocationStore _store;

    public InventoryLocationService(ISiteService siteService, StockLocationStore store)
    {
        _siteService = siteService;
        _store = store;
    }

    public async Task<IReadOnlyList<InventoryLocation>> GetLocationsAsync()
    {
        var locations = (await _store.GetAllAsync())
            .OrderBy(location => location.OwnerKind is null ? 0 : 1)
            .ThenBy(location => location.Position)
            .ThenBy(location => location.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(location => new InventoryLocation
            {
                LocationId = location.ItemId,
                Name = location.Name,
                Address = location.Address,
                OwnerKind = location.OwnerKind,
                OwnerId = location.OwnerId,
            })
            .ToList();
        var known = locations.Select(location => location.LocationId).ToHashSet(StringComparer.Ordinal);

        foreach (var legacy in (await _siteService.GetSettingsAsync<InventorySettings>()).Locations ?? [])
        {
            if (!string.IsNullOrEmpty(legacy?.LocationId) && known.Add(legacy.LocationId))
            {
                locations.Add(legacy);
            }
        }

        if (!known.Contains(InventoryConstants.DefaultLocationId))
        {
            locations.Insert(0, new InventoryLocation { LocationId = InventoryConstants.DefaultLocationId, Name = DefaultLocationName });
        }

        return locations;
    }

    async Task<IReadOnlyList<FulfillmentLocation>> IFulfillmentLocationProvider.GetLocationsAsync()
        => (await GetLocationsAsync()).Where(location => location.OwnerKind is null).Select(location => new FulfillmentLocation(location.LocationId, location.Name)).ToArray();
}
