using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Inventory.ViewModels;

public class InventoryPartViewModel
{
    public bool TrackInventory { get; set; }

    public bool AllowBackorder { get; set; }

    public int? LowStockThreshold { get; set; }

    /// <summary>
    /// Gets or sets when the product ships, in the site's time zone.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    [BindNever]
    public int DefaultLowStockThreshold { get; set; }

    [BindNever]
    public IReadOnlyList<StockItem> StockItems { get; set; } = [];

    [BindNever]
    public bool IsNew { get; set; }
}

public class InventorySettingsViewModel
{
    public int DefaultLowStockThreshold { get; set; }

    public int HoldMinutes { get; set; }

    public string[] LowStockNotificationRoles { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> Roles { get; set; } = [];

    public List<InventoryLocationViewModel> Locations { get; set; } = [];
}

public class InventoryLocationViewModel
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public string Address { get; set; }

    public bool Remove { get; set; }

    [BindNever]
    public bool IsDefault { get; set; }
}

public class InventoryIndexOptions
{
    public string Search { get; set; }

    public StockItemFilter Filter { get; set; }

    [BindNever]
    public List<SelectListItem> Filters { get; set; } = [];
}

public class InventoryIndexViewModel
{
    public InventoryIndexOptions Options { get; set; }

    public IReadOnlyList<StockItem> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public bool CanManage { get; set; }

    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the stock items, pager and filters the <c>admin-list</c> tag renders, in the layout (List or
    /// Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

public class StockItemButtonsViewModel
{
    public StockItem Item { get; set; }

    public bool CanManage { get; set; }
}

public class StockItemViewModel
{
    public StockItem Item { get; set; }

    public IReadOnlyList<StockMovement> Movements { get; set; } = [];

    public IReadOnlyList<StockReservation> Holds { get; set; } = [];

    public int MovementCount { get; set; }

    /// <summary>
    /// Gets or sets how many shoppers asked to hear when it's back, or <see langword="null"/> without Back-in-Stock
    /// Alerts.
    /// </summary>
    public int? WaitingAlerts { get; set; }

    public bool CanManage { get; set; }

    public dynamic Pager { get; set; }

    public AdjustStockViewModel Adjust { get; set; } = new();

    public CountStockViewModel Count { get; set; } = new();

    public TransferStockViewModel Transfer { get; set; } = new();

    /// <summary>
    /// Gets or sets the locations, with any no longer listed that still hold units of the item.
    /// </summary>
    public IReadOnlyList<InventoryLocation> Locations { get; set; } = [];

    /// <summary>
    /// Gets or sets the locations listed in the settings, which units can be received at, counted at and moved to.
    /// </summary>
    public IReadOnlyList<InventoryLocation> ListedLocations { get; set; } = [];

    public bool HasLocations
        => Locations.Count > 1;

    public string LocationName(string locationId)
        => Locations.FirstOrDefault(location => location.LocationId == (string.IsNullOrEmpty(locationId) ? CrestApps.OrchardCore.Inventory.Core.InventoryConstants.DefaultLocationId : locationId))?.Name ?? locationId;
}

public class AdjustStockViewModel
{
    public string LocationId { get; set; }

    public StockMovementKind Kind { get; set; } = StockMovementKind.Received;

    public int Quantity { get; set; }

    public string Reason { get; set; }
}

public class CountStockViewModel
{
    public string LocationId { get; set; }

    public int? Count { get; set; }

    public string Reason { get; set; }
}

public class TransferStockViewModel
{
    public string FromLocationId { get; set; }

    public string ToLocationId { get; set; }

    public int? Quantity { get; set; }

    public string Reason { get; set; }
}

/// <summary>
/// The back-in-stock form on a sold-out product's page.
/// </summary>
public class StockAlertFormViewModel
{
    public string ContentItemId { get; set; }

    public bool HasVariants { get; set; }

    /// <summary>
    /// Gets or sets the variants that are sold out, for a product sold in variants: the form shows when one is picked.
    /// </summary>
    public IList<string> SoldOutVariantIds { get; set; } = [];

    public string Email { get; set; }

    public string ReturnUrl { get; set; }
}

public class PurchaseOrderIndexOptions
{
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the orders shown: a status, <c>all</c>, or empty for the open ones.
    /// </summary>
    public string Show { get; set; }

    [BindNever]
    public List<SelectListItem> Filters { get; set; } = [];
}

public class PurchaseOrderIndexViewModel
{
    public PurchaseOrderIndexOptions Options { get; set; }

    public AdminListContext List { get; set; }

    public bool CanManage { get; set; }
}

public class PurchaseOrderEditViewModel
{
    public string PurchaseOrderId { get; set; }

    public string Supplier { get; set; }

    public string SupplierEmail { get; set; }

    public string LocationId { get; set; }

    public string Currency { get; set; }

    public DateTime? ExpectedDate { get; set; }

    public string Note { get; set; }

    /// <summary>
    /// Gets or sets whether a new order starts with the items that are low on stock at its location.
    /// </summary>
    public bool StartWithLowStock { get; set; }

    public List<PurchaseOrderLineViewModel> Lines { get; set; } = [];

    public string[] AddStockItemIds { get; set; } = [];

    [BindNever]
    public PurchaseOrder Order { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Locations { get; set; } = [];

    [BindNever]
    public string LocationName { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Currencies { get; set; } = [];

    [BindNever]
    public IReadOnlyList<string> Suppliers { get; set; } = [];

    [BindNever]
    public bool CanManage { get; set; }

    [BindNever]
    public string SearchEndpoint { get; set; }
}

public class PurchaseOrderLineViewModel
{
    public string LineId { get; set; }

    public int? Ordered { get; set; }

    public decimal? UnitCost { get; set; }

    public bool Remove { get; set; }

    /// <summary>
    /// Gets or sets how many of the line arrived, when receiving.
    /// </summary>
    public int? Receive { get; set; }
}

public class PurchaseOrderRowViewModel
{
    public PurchaseOrder Order { get; set; }

    public string LocationName { get; set; }

    public bool CanManage { get; set; }
}
