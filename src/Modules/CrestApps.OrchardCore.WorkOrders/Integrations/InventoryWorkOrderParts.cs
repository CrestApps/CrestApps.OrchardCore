using System.Globalization;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// Takes parts used on a work order off their Inventory location, with the work order as the reference.
/// </summary>
internal sealed class InventoryWorkOrderParts : IWorkOrderPartsService
{
    private readonly IInventoryService _inventory;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryWorkOrderParts"/> class.
    /// </summary>
    /// <param name="inventory">The inventory service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InventoryWorkOrderParts(IInventoryService inventory, IStringLocalizer<InventoryWorkOrderParts> stringLocalizer)
    {
        _inventory = inventory;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<string> UseAsync(WorkOrder workOrder, WorkOrderLine line, decimal quantity, string actor, CancellationToken cancellationToken = default)
    {
        if (quantity != decimal.Truncate(quantity))
        {
            return S["Counted parts are used in whole units."];
        }

        var result = await _inventory.ConsumeAsync(line.StockItemId, line.LocationId, (int)quantity, WorkOrdersConstants.ReferenceType, workOrder.ItemId, workOrder.Number, new StockActor(null, actor), cancellationToken);

        return result.Error switch
        {
            StockChangeError.None => null,
            StockChangeError.NotEnoughAtLocation => S["There aren't {0} of {1} available there.", quantity.ToString("0", CultureInfo.CurrentCulture), line.Description],
            StockChangeError.NotFound => S["{0} isn't counted in Inventory.", line.Description],
            StockChangeError.Busy => S["Someone else changed {0} at the same moment. Try again.", line.Description],
            _ => S["Inventory refused the parts."],
        };
    }
}
