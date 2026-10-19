using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// The places stock is kept, in the order orders take units from them.
/// </summary>
public interface IInventoryLocationService
{
    Task<IReadOnlyList<InventoryLocation>> GetLocationsAsync();
}
