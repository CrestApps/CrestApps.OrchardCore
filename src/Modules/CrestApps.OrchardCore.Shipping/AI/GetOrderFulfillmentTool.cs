using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Returns what an order still has to ship (<see cref="IShipmentService.GetRemainingAsync"/>) and its shipments
/// (<see cref="IShipmentStore.ListByOrderAsync"/>), as the Shipments card of the order's page shows them.
/// </summary>
internal sealed class GetOrderFulfillmentTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getOrderFulfillment";

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
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns, for one order, the lines and quantities still to ship and its shipments with number, status, lines, carrier, tracking number and link, tracking events, label cost and dates. Use it before creating or changing a shipment.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        var remaining = await services.GetRequiredService<IShipmentService>().GetRemainingAsync(order, cancellationToken);
        var shipments = await services.GetRequiredService<IShipmentStore>().ListByOrderAsync(order.ItemId, cancellationToken);
        var described = new List<object>();

        foreach (var shipment in shipments.OrderBy(shipment => shipment.Number))
        {
            described.Add(await DescribeAsync(services, shipment));
        }

        return Json(new
        {
            orderId = order.ItemId,
            orderNumber = order.OrderNumber,
            orderStatus = ToCamel(order.Status.ToString()),
            fulfillmentStatus = order.FulfillmentStatus is { } status ? ToCamel(status.ToString()) : null,
            toShip = order.Lines
                .Where(line => remaining.TryGetValue(line.LineId, out var left) && left > 0)
                .Select(line => new { lineId = line.LineId, title = line.Title, sku = line.Sku, ordered = line.Quantity, remaining = remaining[line.LineId] }),
            shipments = described,
        });
    }
}
