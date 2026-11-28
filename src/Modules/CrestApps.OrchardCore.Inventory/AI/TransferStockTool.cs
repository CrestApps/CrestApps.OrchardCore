using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Moves units of a stock item between locations as the Transfer form of its Inventory page does
/// (<see cref="IInventoryService.TransferAsync"/>): only available units move, the total on hand does not change, units
/// can come from a location no longer listed but only go to a listed one, and the reason defaults to "From A to B". The
/// change is committed at once. It needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class TransferStockTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "transferStock";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "stockItemId": {
          "type": "string",
          "description": "The stock item's id, from listStockItems or getStockItem."
        },
        "fromLocationId": {
          "type": "string",
          "description": "The location the units come from, from listStockLocations or the item's levels in getStockItem (a removed location still holding units can be emptied)."
        },
        "toLocationId": {
          "type": "string",
          "description": "The listed location the units go to, from listStockLocations."
        },
        "quantity": {
          "type": "integer",
          "description": "How many units to move, more than zero."
        },
        "reason": {
          "type": "string",
          "description": "Why they move. 'From <location> to <location>' when left out."
        }
      },
      "required": ["stockItemId", "fromLocationId", "toLocationId", "quantity"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Moves units of a stock item from one location to another without changing the total on hand. Only units that are available (not held for orders) can move, and the destination must be a listed location.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (item, error) = await FindItemAsync(services, ReadString(arguments, "stockItemId"), cancellationToken);

        if (item is null)
        {
            return error;
        }

        var fromLocationId = ReadString(arguments, "fromLocationId");
        var toLocationId = ReadString(arguments, "toLocationId");
        var quantity = ReadInt(arguments, "quantity");

        if (quantity is not > 0 || fromLocationId is null || toLocationId is null)
        {
            return "Nothing was changed: choose where the units come from and go to, and how many (more than zero).";
        }

        if (fromLocationId == toLocationId)
        {
            return "Nothing was changed: choose two different locations.";
        }

        // Units can come from a location no longer listed, but only go to one that is.
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var to = locations.FirstOrDefault(location => location.LocationId == toLocationId);

        if (to is null)
        {
            return $"Nothing was changed: there is no listed stock location with the id '{toLocationId}'. Use listStockLocations for the ids.";
        }

        var from = locations.FirstOrDefault(location => location.LocationId == fromLocationId)?.Name;

        if (from is null && item.FindLevel(fromLocationId) is null)
        {
            return $"Nothing was changed: there is no stock location with the id '{fromLocationId}' holding this item. Use getStockItem for the item's levels.";
        }

        from ??= fromLocationId;

        var reason = ReadString(arguments, "reason") ?? $"From {from} to {to.Name}";
        var inventory = services.GetRequiredService<IInventoryService>();
        var (result, changeError) = await ChangeStockAsync(services, () => inventory.TransferAsync(item.ItemId, fromLocationId, toLocationId, quantity.Value, reason, StockActor.ForUser(caller), cancellationToken), cancellationToken);

        if (result is null)
        {
            return changeError.Contains("available to move", StringComparison.Ordinal)
                ? $"Nothing was changed: {from} doesn't have {quantity} available to move. Units held for orders stay where they are."
                : changeError;
        }

        return Json(new
        {
            item = Describe(result.Item, locations),
            moved = quantity,
            from,
            to = to.Name,
            reason,
            message = $"Moved {quantity} from {from} to {to.Name}.",
        });
    }
}
