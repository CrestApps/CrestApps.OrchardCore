using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// The stock location list of Settings › Commerce › Inventory, shared with the stock location AI tools: the site's own
/// locations (not those owned by another record, such as a van), at most <see cref="MaxLocations"/>, each with a name, in
/// the order orders take units from them; the default location is never removed, and a location keeps its id because
/// stock levels and movements point at it.
/// </summary>
internal static class StockLocationEditor
{
    /// <summary>
    /// The most locations a site lists.
    /// </summary>
    public const int MaxLocations = 50;

    /// <summary>
    /// Gets the locations the list edits: the site's own, in order. Locations owned by other records are managed with
    /// their owner.
    /// </summary>
    /// <param name="locations">Every location, from <see cref="IInventoryLocationService.GetLocationsAsync"/>.</param>
    /// <returns>The site's own locations.</returns>
    public static List<InventoryLocation> GetSiteLocations(IEnumerable<InventoryLocation> locations)
        => [.. (locations ?? []).Where(location => location.OwnerKind is null)];

    /// <summary>
    /// Reads the rows of the list. A blank row, such as the one to add a location with, is dropped, and so is one marked
    /// for removal, except the default location: stock counted before there were locations is there. A removed location's
    /// units stay on its items, where they can still be sold and moved to another location.
    /// </summary>
    /// <param name="rows">The rows, in order.</param>
    /// <returns>The locations to keep, and the indexes of the rows that need a name.</returns>
    public static (List<InventoryLocation> Locations, List<int> RowsWithoutName) Parse(IList<InventoryLocationViewModel> rows)
    {
        var locations = new List<InventoryLocation>();
        var withoutName = new List<int>();

        for (var i = 0; i < (rows?.Count ?? 0); i++)
        {
            var row = rows[i];
            var isDefault = row?.LocationId == InventoryConstants.DefaultLocationId;

            if (row is null || (row.Remove && !isDefault) || (!isDefault && string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.Address)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                withoutName.Add(i);
            }

            locations.Add(new InventoryLocation
            {
                LocationId = string.IsNullOrWhiteSpace(row.LocationId) ? UniqueId.GenerateId() : row.LocationId.Trim(),
                Name = row.Name?.Trim(),
                Address = string.IsNullOrWhiteSpace(row.Address) ? null : row.Address.Trim(),
            });
        }

        return (locations.DistinctBy(location => location.LocationId, StringComparer.Ordinal).Take(MaxLocations).ToList(), withoutName);
    }

    /// <summary>
    /// Adds, renames, reorders and removes the site's own locations in the store, keeping their ids, so stock levels
    /// and movements still point at them. Their order in the list is the order orders take units from them.
    /// </summary>
    /// <param name="store">The location store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="locations">The site's own locations, in order.</param>
    /// <returns>The task.</returns>
    public static async Task SaveAsync(StockLocationStore store, IClock clock, IReadOnlyList<InventoryLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        var stored = (await store.GetAllAsync()).Where(location => location.OwnerKind is null).ToDictionary(location => location.ItemId, StringComparer.Ordinal);

        for (var i = 0; i < (locations?.Count ?? 0); i++)
        {
            var location = locations[i];

            if (stored.Remove(location.LocationId, out var existing))
            {
                existing.Name = location.Name;
                existing.Address = location.Address;
                existing.Position = i + 1;
                await store.UpdateAsync(existing);
            }
            else
            {
                await store.CreateAsync(new StockLocation
                {
                    ItemId = location.LocationId,
                    Name = location.Name,
                    Address = location.Address,
                    Position = i + 1,
                    CreatedUtc = clock.UtcNow,
                });
            }
        }

        foreach (var removed in stored.Values.Where(location => location.ItemId != InventoryConstants.DefaultLocationId))
        {
            await store.DeleteAsync(removed);
        }
    }
}
