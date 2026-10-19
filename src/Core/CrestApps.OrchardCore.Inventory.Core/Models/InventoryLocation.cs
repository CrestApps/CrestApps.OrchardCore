namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// A place stock is kept, such as a warehouse or a shop. Orders take their units from the locations in the order they
/// are listed in, unless the order says where it's fulfilled from, such as the shop it's picked up at.
/// </summary>
public sealed class InventoryLocation
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where the location is, on one line, for staff.
    /// </summary>
    public string Address { get; set; }
}
