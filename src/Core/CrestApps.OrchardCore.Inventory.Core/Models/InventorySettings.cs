namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// Settings › Commerce › Inventory.
/// </summary>
public sealed class InventorySettings
{
    /// <summary>
    /// Gets or sets how few available units count as low stock for products without their own threshold.
    /// </summary>
    public int DefaultLowStockThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many minutes stock stays held for an order that hasn't been paid for. A payment that arrives
    /// later still sells the units, below zero if they were sold to someone else meanwhile.
    /// </summary>
    public int HoldMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the roles whose users are notified when a product runs low or sells out. Notifications need
    /// Orchard Core's Notifications feature.
    /// </summary>
    public string[] LowStockNotificationRoles { get; set; } = ["Administrator"];

    /// <summary>
    /// Gets or sets the places stock is kept, in the order orders take units from them. The default location, where
    /// stock counted before there were locations is, is always one of them; see <see cref="GetLocations"/>.
    /// </summary>
    public List<InventoryLocation> Locations { get; set; } = [];

    /// <summary>
    /// Gets the locations, the default one included even when it was never named.
    /// </summary>
    public IReadOnlyList<InventoryLocation> GetLocations()
    {
        var locations = (Locations ?? []).Where(location => !string.IsNullOrEmpty(location?.LocationId)).ToList();

        if (!locations.Any(location => location.LocationId == InventoryConstants.DefaultLocationId))
        {
            locations.Insert(0, new InventoryLocation { LocationId = InventoryConstants.DefaultLocationId, Name = "Main location" });
        }

        return locations;
    }
}
