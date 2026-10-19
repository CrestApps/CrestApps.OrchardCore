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

    public bool CanManage { get; set; }

    public dynamic Pager { get; set; }

    public AdjustStockViewModel Adjust { get; set; } = new();

    public CountStockViewModel Count { get; set; } = new();
}

public class AdjustStockViewModel
{
    public StockMovementKind Kind { get; set; } = StockMovementKind.Received;

    public int Quantity { get; set; }

    public string Reason { get; set; }
}

public class CountStockViewModel
{
    public int? Count { get; set; }

    public string Reason { get; set; }
}
