using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Creates a shipment to pack for a paid order, from some or all of the lines still to ship, through the service the
/// order page's Create shipment form calls (<see cref="IShipmentService.CreateAsync"/>, not shipped). It stays pending
/// until <c>shipShipment</c> marks it shipped. Committed at once.
/// </summary>
internal sealed class CreateShipmentTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "createShipment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The order's id, from listOrders."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, when the id is not known."
        },
        "lines": {
          "type": "array",
          "description": "The order lines and how many of each go in the shipment, from getOrderFulfillment's toShip.",
          "items": {
            "type": "object",
            "properties": {
              "lineId": { "type": "string", "description": "The order line's id." },
              "quantity": { "type": "integer", "description": "How many of it, at least 1." }
            },
            "required": ["lineId", "quantity"],
            "additionalProperties": false
          }
        },
        "everything": {
          "type": "boolean",
          "description": "Set to true to put everything still to ship in the shipment, instead of listing lines."
        },
        "note": {
          "type": "string",
          "description": "A note for the packer, kept with the shipment."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a shipment to pack for a paid order from some or all of the lines still to ship, with an optional note, and leaves it pending. Use shipShipment when it leaves.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadLines(arguments, out var lines))
        {
            return "Nothing was done: lines must be a list of { lineId, quantity } with a whole quantity of at least 1.";
        }

        var everything = ReadBool(arguments, "everything") == true;

        if (everything == (lines is { Count: > 0 }))
        {
            return "Nothing was done: give the lines to ship, or everything set to true, not both.";
        }

        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        var shipmentService = services.GetRequiredService<IShipmentService>();

        if (everything)
        {
            lines = (await shipmentService.GetRemainingAsync(order, cancellationToken))
                .Where(entry => entry.Value > 0)
                .Select(entry => new ShipmentLineRequest(entry.Key, entry.Value))
                .ToList();
        }

        var result = await shipmentService.CreateAsync(order, lines, new ShipmentDetails(null, null, Note: ReadString(arguments, "note")), ship: false, cancellationToken);

        if (!result.Succeeded)
        {
            return Explain(result.Error);
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            shipment = await DescribeAsync(services, result.Shipment),
            message = $"Shipment {result.Shipment.Number} is ready to pack. Mark it shipped with shipShipment when it leaves.",
        });
    }
}
