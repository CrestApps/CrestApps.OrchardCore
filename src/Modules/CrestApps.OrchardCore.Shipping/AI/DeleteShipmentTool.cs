using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Removes a shipment that has not left yet and puts its items back among those still to ship, through the Delete
/// action of the order page (<see cref="IShipmentService.DeleteAsync"/>). Committed at once.
/// </summary>
internal sealed class DeleteShipmentTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteShipment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "shipmentId": {
          "type": "string",
          "description": "The pending shipment's id, from getOrderFulfillment."
        }
      },
      "required": ["shipmentId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a shipment that has not left yet and puts its items back among those still to ship.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (shipment, error) = await FindShipmentAsync(services, arguments, cancellationToken);

        if (shipment is null)
        {
            return error;
        }

        var result = await services.GetRequiredService<IShipmentService>().DeleteAsync(shipment, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was done: shipment {shipment.Number} is {ToCamel(shipment.Status.ToString())}; only a shipment that has not left can be removed.";
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            id = shipment.ItemId,
            number = shipment.Number,
            orderNumber = shipment.OrderNumber,
            message = $"Shipment {shipment.Number} was removed, and its items put back among those still to ship.",
        });
    }
}
