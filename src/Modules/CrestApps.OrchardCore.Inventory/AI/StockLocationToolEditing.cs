using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Saves the stock location list for the location tools as Settings › Commerce › Inventory does: the rows go through
/// <see cref="StockLocationEditor.Parse"/> and <see cref="StockLocationEditor.SaveAsync"/>, and the locations still kept
/// in the settings from before locations had a store are cleared once they are saved to it.
/// </summary>
internal static class StockLocationToolEditing
{
    /// <summary>
    /// Gets the rows of the list as the settings page shows them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The site's own locations, in order, as rows.</returns>
    public static async Task<List<InventoryLocationViewModel>> GetRowsAsync(IServiceProvider services)
        => [.. StockLocationEditor.GetSiteLocations(await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync())
            .Select(location => new InventoryLocationViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                Address = location.Address,
            })];

    /// <summary>
    /// Saves the rows.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="rows">The rows, in order.</param>
    /// <returns>The locations saved, or <see langword="null"/> when a row has no name.</returns>
    public static async Task<IReadOnlyList<InventoryLocation>> SaveAsync(IServiceProvider services, List<InventoryLocationViewModel> rows)
    {
        var (locations, rowsWithoutName) = StockLocationEditor.Parse(rows);

        if (rowsWithoutName.Count > 0)
        {
            return null;
        }

        await StockLocationEditor.SaveAsync(services.GetRequiredService<StockLocationStore>(), services.GetRequiredService<IClock>(), locations);

        // The locations live in the store now; the settings keep none, as after a save of the settings page.
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();

        if (site.TryGet<InventorySettings>(out var settings) && settings.Locations?.Count > 0)
        {
            settings.Locations = [];
            site.Put(settings);
            await siteService.UpdateSiteSettingsAsync(site);
        }

        return locations;
    }
}
