using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Changes a stock item's units at one location as the Receive, Adjust and Count forms of its Inventory page do
/// (<see cref="IInventoryService.AdjustAsync(string, string, StockMovementKind, int, string, StockActor, CancellationToken)"/>
/// and <see cref="IInventoryService.SetCountAsync(string, string, int, string, StockActor, CancellationToken)"/>): each
/// change is a movement recorded with the caller and the reason, and is committed at once. Stricter than the forms: a
/// reason is required, and a location the store does not list is refused instead of being replaced by the default one.
/// It needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class AdjustStockTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "adjustStock";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "stockItemId": {
          "type": "string",
          "description": "The stock item's id, from listStockItems or getStockItem."
        },
        "kind": {
          "type": "string",
          "enum": ["receive", "adjust", "count"],
          "description": "receive adds units that arrived (quantity more than zero); adjust corrects up or down (quantity not zero, negative to take away); count sets the units on hand at the location to what was counted (quantity zero or more)."
        },
        "quantity": {
          "type": "integer",
          "description": "The units received, the change up or down, or the units counted."
        },
        "locationId": {
          "type": "string",
          "description": "The stock location, from listStockLocations. The default location when left out."
        },
        "reason": {
          "type": "string",
          "description": "Why the stock changes, such as 'Delivery 42' or 'Two damaged in the shop'. It is kept with the movement."
        }
      },
      "required": ["stockItemId", "kind", "quantity", "reason"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes a stock item's units at one location: receive adds units that arrived, adjust corrects up or down, count sets the units on hand to what was counted. Each change is recorded as a movement with its reason; if someone else changed the same item at the same moment it reports busy and nothing is saved.";

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

        var kind = ReadString(arguments, "kind")?.ToLowerInvariant();
        var quantity = ReadInt(arguments, "quantity");
        var reason = ReadString(arguments, "reason");

        switch (kind)
        {
            case "receive" when quantity is not > 0:
                return "Nothing was changed: give the units received, more than zero.";
            case "adjust" when quantity is null or 0:
                return "Nothing was changed: give the change in units, up or down, not zero.";
            case "count" when quantity is not >= 0:
                return "Nothing was changed: give what was counted, zero or more.";
            case "receive" or "adjust" or "count":
                break;
            default:
                return "Nothing was changed: kind must be receive, adjust or count.";
        }

        if (reason is null)
        {
            return "Nothing was changed: say why the stock changes. The reason is kept with the movement.";
        }

        var (locationId, locationName, locationError) = await GetListedLocationAsync(services, ReadString(arguments, "locationId"));

        if (locationId is null)
        {
            return locationError;
        }

        var inventory = services.GetRequiredService<IInventoryService>();
        var actor = StockActor.ForUser(caller);
        var (result, changeError) = await ChangeStockAsync(services, () => kind == "count"
            ? inventory.SetCountAsync(item.ItemId, locationId, quantity.Value, reason, actor, cancellationToken)
            : inventory.AdjustAsync(item.ItemId, locationId, kind == "receive" ? StockMovementKind.Received : StockMovementKind.Adjusted, quantity.Value, reason, actor, cancellationToken), cancellationToken);

        if (result is null)
        {
            return changeError;
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(new
        {
            item = Describe(result.Item, locations),
            location = locationName,
            movement = result.Movement is null
                ? null
                : new
                {
                    kind = result.Movement.Kind.ToString(),
                    quantity = result.Movement.Quantity,
                    onHandAfter = result.Movement.OnHandAfter,
                    reason = result.Movement.Reason,
                    by = result.Movement.ActorName,
                    when = await LocalTimeAsync(services, result.Movement.Utc),
                },
            message = kind switch
            {
                "receive" => $"{quantity} received at {locationName}. {result.Item.OnHand} on hand.",
                "adjust" => $"Adjusted by {quantity} at {locationName}. {result.Item.OnHand} on hand.",
                _ => $"Counted {quantity} at {locationName}. The difference was {result.Movement?.Quantity ?? 0}.",
            },
        });
    }
}
