using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Receives the items of an ordered purchase order as the Receive form of Commerce › Purchase orders does
/// (<see cref="PurchaseOrderReceiver.ReceiveAsync"/>): by line and quantity, or everything still outstanding, each never
/// more than is still to come, added to stock at the order's location as movements with the caller as actor. It is
/// committed at once, as the page does, so stock someone else changed at the same moment is reported instead of
/// overwritten. It needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class ReceivePurchaseOrderTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "receivePurchaseOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The ordered purchase order, by id or number."
        },
        "all": {
          "type": "boolean",
          "description": "Receive everything still outstanding on every line."
        },
        "lines": {
          "type": "array",
          "description": "What arrived, by line, when not everything did.",
          "items": {
            "type": "object",
            "properties": {
              "lineId": {
                "type": "string",
                "description": "The line, from getPurchaseOrder."
              },
              "quantity": {
                "type": "integer",
                "description": "The units that arrived, more than zero; at most what is still outstanding is received."
              }
            },
            "required": ["lineId", "quantity"],
            "additionalProperties": false
          }
        }
      },
      "required": ["purchaseOrderId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records items of an ordered purchase order as arrived, by line and quantity or everything still outstanding, adding them to stock at the order's location. The order becomes received once nothing is outstanding.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, ReadString(arguments, "purchaseOrderId"), cancellationToken);

        if (order is null)
        {
            return error;
        }

        if (!order.CanReceive)
        {
            return order.IsDraft
                ? $"Nothing was received: stock can be received once purchase order {order.Number} is marked ordered (markPurchaseOrderOrdered)."
                : $"Nothing was received: purchase order {order.Number} is {StatusName(order.Status)}.";
        }

        var all = ReadJson(arguments, "all") is { ValueKind: JsonValueKind.True };
        var (quantities, linesError) = ReadQuantities(arguments, order);

        if (linesError is not null)
        {
            return linesError;
        }

        if (all == (quantities.Count > 0))
        {
            return "Nothing was received: give either all: true or the lines that arrived, not both and not neither.";
        }

        if (all)
        {
            quantities = order.Lines.ToDictionary(line => line.LineId, line => line.Outstanding);
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var received = await services.GetRequiredService<PurchaseOrderReceiver>().ReceiveAsync(order, quantities, StockActor.ForUser(caller), cancellationToken);

        if (received == 0)
        {
            return "Nothing was received: nothing given is still outstanding on those lines, or their stock items no longer exist.";
        }

        if (await CommitAsync(services, order: null, cancellationToken) is not null)
        {
            return "Nothing was recorded: the stock changed while it was being received. Read the purchase order again with getPurchaseOrder and retry in your next message.";
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var location = locations.FirstOrDefault(entry => entry.LocationId == order.LocationId)?.Name ?? order.LocationId;

        return Json(new
        {
            purchaseOrder = await DescribeAsync(services, order, locations),
            received,
            message = order.Status == PurchaseOrderStatus.Received
                ? $"Received {received} at {location}. Purchase order {order.Number} is complete."
                : $"Received {received} at {location}. {order.Ordered - order.Received} still to come.",
        });
    }

    private static (Dictionary<string, int> Quantities, string Error) ReadQuantities(AIFunctionArguments arguments, PurchaseOrder order)
    {
        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);

        if (ReadJson(arguments, "lines") is not { } element)
        {
            return (quantities, null);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return (null, "Nothing was received: lines must be a list of lineId and quantity.");
        }

        foreach (var entry in element.EnumerateArray())
        {
            var lineId = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("lineId", out var id) ? ToText(id) : null;
            var quantity = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("quantity", out var value) ? ToInt(value) : null;

            if (lineId is null || order.Lines.All(line => line.LineId != lineId))
            {
                return (null, $"Nothing was received: the purchase order has no line '{lineId}'. Use getPurchaseOrder for its line ids.");
            }

            if (quantity is not > 0)
            {
                return (null, "Nothing was received: give the units that arrived on each line, more than zero.");
            }

            // As the page: what arrived on one line in one go is capped, and never more than is still to come.
            quantities[lineId] = Math.Min(PurchaseOrderRules.MaxQuantity, (quantities.TryGetValue(lineId, out var earlier) ? earlier : 0) + quantity.Value);
        }

        return (quantities, null);
    }
}
