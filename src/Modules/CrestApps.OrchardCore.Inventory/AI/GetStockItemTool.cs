using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Returns one stock item as its page on Commerce › Inventory shows it: the level at each location, the units held for
/// unpaid orders (<see cref="StockChanges.ListHoldsAsync"/>), the shoppers waiting for a back-in-stock email when that
/// feature is on, and a page of its movements, newest first, with their times in the store's time zone. It needs
/// <see cref="InventoryPermissions.ViewInventory"/>.
/// </summary>
internal sealed class GetStockItemTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getStockItem";

    private const int MaxHolds = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "stockItemId": {
          "type": "string",
          "description": "The stock item's id, from listStockItems."
        },
        "sku": {
          "type": "string",
          "description": "The SKU of the product or variant, ignoring case, when the id is not known. A SKU that more than one counted item shares finds none."
        },
        "contentItemId": {
          "type": "string",
          "description": "The product's content item id, with variantId for a variant, when neither the id nor the SKU is known."
        },
        "variantId": {
          "type": "string",
          "description": "The variant's id, with contentItemId."
        },
        "page": {
          "type": "integer",
          "description": "The page of the movement history, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Movements per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one stock item, found by id, by SKU, or by product and variant, with its level at each location, the units held for unpaid orders, how many shoppers wait for a back-in-stock email, and a page of its movement history. Use it before changing stock.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ViewInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IStockItemStore>();
        var stockItemId = ReadString(arguments, "stockItemId");
        var sku = ReadString(arguments, "sku");
        var contentItemId = ReadString(arguments, "contentItemId");
        StockItem item;

        if (stockItemId is not null)
        {
            item = await store.FindByIdAsync(stockItemId, cancellationToken);
        }
        else if (sku is not null)
        {
            item = await store.FindBySkuAsync(sku, cancellationToken: cancellationToken);
        }
        else if (contentItemId is not null)
        {
            item = await store.FindAsync(contentItemId, ReadString(arguments, "variantId"), cancellationToken: cancellationToken);
        }
        else
        {
            return "Nothing was found: give stockItemId, sku, or contentItemId (with variantId for a variant).";
        }

        if (item is null)
        {
            return sku is not null && stockItemId is null
                ? $"Nothing was found: no counted product or variant has the SKU '{sku}', or more than one shares it. Use listStockItems with the SKU as query to find it."
                : "Nothing was found: no stock item matches. Use listStockItems to find it.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (movementCount, movements) = await services.GetRequiredService<IStockMovementStore>().PageAsync(item.ItemId, (page - 1) * pageSize, pageSize, cancellationToken);
        var holds = await StockChanges.ListHoldsAsync(services.GetRequiredService<YesSqlSession>(), item.ItemId, MaxHolds, cancellationToken);
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var alerts = services.GetService<StockAlertService>();

        var history = new List<object>();

        foreach (var movement in movements)
        {
            history.Add(new
            {
                kind = movement.Kind.ToString(),
                quantity = movement.Quantity,
                onHandAfter = movement.OnHandAfter,
                location = movement.LocationId is null ? null : locations.FirstOrDefault(location => location.LocationId == movement.LocationId)?.Name ?? "Removed location",
                reason = movement.Reason,
                by = movement.ActorName,
                orderId = movement.OrderId,
                purchaseOrderId = movement.PurchaseOrderId,
                when = await LocalTimeAsync(services, movement.Utc),
            });
        }

        var heldFor = new List<object>();

        foreach (var hold in holds)
        {
            heldFor.Add(new
            {
                orderId = hold.OrderId,
                quantity = hold.Quantity,
                expires = await LocalTimeAsync(services, hold.ExpiresUtc),
            });
        }

        return Json(new
        {
            item = Describe(item, locations),
            holds = heldFor,
            waitingForBackInStockEmail = alerts is null ? (int?)null : await alerts.CountWaitingAsync(item.ItemId, cancellationToken),
            movements = history,
            movementCount,
            page,
            pageSize,
            truncated = page * pageSize < movementCount,
        });
    }
}
