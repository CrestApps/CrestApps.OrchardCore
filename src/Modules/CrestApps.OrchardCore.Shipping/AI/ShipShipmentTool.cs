using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Marks a pending shipment as shipped with its carrier and tracking (<see cref="IShipmentService.ShipAsync"/>, the
/// Ship action of the order page), or creates and ships in one step everything an order still has to ship
/// (<see cref="IShipmentService.CreateAsync"/> with ship). When shipped emails are on, the buyer is emailed that the
/// order is on its way, so it asks for approval. Committed at once.
/// </summary>
internal sealed class ShipShipmentTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "shipShipment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "shipmentId": {
          "type": "string",
          "description": "The pending shipment to mark shipped, from getOrderFulfillment."
        },
        "orderId": {
          "type": "string",
          "description": "Instead of a shipment: the order whose remaining lines are all shipped at once in a new shipment."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, instead of orderId."
        },
        "carrier": {
          "type": "string",
          "description": "The carrier, such as UPS."
        },
        "trackingNumber": {
          "type": "string",
          "description": "The tracking number."
        },
        "trackingUrl": {
          "type": "string",
          "description": "The tracking link, if the carrier gave one."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a pending shipment as shipped with its carrier, tracking number and optional tracking link; or, given an order instead, creates and ships in one step everything still to ship on it. The buyer is emailed that the order is on its way when shipped emails are on.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var shipmentId = ReadString(arguments, "shipmentId");
        var byOrder = ReadString(arguments, "orderId") is not null || ReadString(arguments, "orderNumber") is not null;

        if ((shipmentId is null) == !byOrder)
        {
            return "Nothing was done: give the pending shipment's id, or the order whose remaining lines all ship now, not both.";
        }

        var shipmentService = services.GetRequiredService<IShipmentService>();
        ShipmentResult result;

        if (shipmentId is not null)
        {
            var (shipment, error) = await FindShipmentAsync(services, arguments, cancellationToken);

            if (shipment is null)
            {
                return error;
            }

            if (shipment.Status != ShipmentStatus.Pending)
            {
                return $"Nothing was done: shipment {shipment.Number} has already left ({ToCamel(shipment.Status.ToString())}).";
            }

            result = await shipmentService.ShipAsync(shipment, new ShipmentDetails(ReadString(arguments, "carrier"), ReadString(arguments, "trackingNumber"), ReadString(arguments, "trackingUrl"), shipment.Note), cancellationToken);
        }
        else
        {
            var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

            if (order is null)
            {
                return error;
            }

            var lines = (await shipmentService.GetRemainingAsync(order, cancellationToken))
                .Where(entry => entry.Value > 0)
                .Select(entry => new ShipmentLineRequest(entry.Key, entry.Value))
                .ToList();

            result = await shipmentService.CreateAsync(order, lines, new ShipmentDetails(ReadString(arguments, "carrier"), ReadString(arguments, "trackingNumber"), ReadString(arguments, "trackingUrl")), ship: true, cancellationToken);
        }

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
            message = $"Shipment {result.Shipment.Number} is on its way. The buyer is emailed when shipped emails are on in the shipping settings.",
        });
    }
}
