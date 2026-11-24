using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Marks a shipped shipment as delivered (<see cref="IShipmentService.DeliverAsync"/>) or as returned so its items can
/// ship again (<see cref="IShipmentService.ReturnAsync"/>), the Deliver and Return actions of the order page. Committed
/// at once.
/// </summary>
internal sealed class UpdateShipmentStatusTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateShipmentStatus";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "shipmentId": {
          "type": "string",
          "description": "The shipment's id, from getOrderFulfillment."
        },
        "status": {
          "type": "string",
          "enum": ["delivered", "returned"],
          "description": "delivered when it arrived; returned when it came back, so its items count as not shipped and can ship again."
        }
      },
      "required": ["shipmentId", "status"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a shipped shipment as delivered, or as returned so its items count as not shipped and can be shipped again.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status")?.ToLowerInvariant();

        if (status is not ("delivered" or "returned"))
        {
            return "Nothing was done: status must be delivered or returned.";
        }

        var (shipment, error) = await FindShipmentAsync(services, arguments, cancellationToken);

        if (shipment is null)
        {
            return error;
        }

        var shipmentService = services.GetRequiredService<IShipmentService>();
        var result = status == "delivered"
            ? await shipmentService.DeliverAsync(shipment, cancellationToken)
            : await shipmentService.ReturnAsync(shipment, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was done: shipment {shipment.Number} is {ToCamel(shipment.Status.ToString())} and can't be marked {status}.";
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            shipment = await DescribeAsync(services, result.Shipment ?? shipment),
            message = status == "delivered"
                ? $"Shipment {shipment.Number} was delivered."
                : $"Shipment {shipment.Number} came back. Its items can be shipped again.",
        });
    }
}
