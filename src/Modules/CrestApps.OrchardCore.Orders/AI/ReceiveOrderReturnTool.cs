using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Records that a return's items came back through the Receive step of its page on Commerce › Returns
/// (<see cref="OrderReturnService.ReceiveAsync"/>): the lines chosen go back into stock, at the location named or where
/// they were sold from, through the orders' event handlers. It does not refund. Committed at once, as on the page.
/// </summary>
internal sealed class ReceiveOrderReturnTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "receiveOrderReturn";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "returnId": {
          "type": "string",
          "description": "The return's id, from listOrderReturns."
        },
        "returnNumber": {
          "type": "string",
          "description": "The return's number, when the id is not known."
        },
        "restockLineIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The lineId of each returned line that goes back into stock, from getOrderReturn. Lines left out are received but not restocked."
        },
        "restockAll": {
          "type": "boolean",
          "description": "Set to true to put every returned line back into stock, instead of listing them."
        },
        "locationId": {
          "type": "string",
          "description": "The stock location the items go back to, from getOrderReturn's restockLocations. Left out, they go back where they were sold from."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records that a return's items came back and puts the chosen lines back into stock, at a named location or where they were sold from. It does not refund.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (orderReturn, error) = await ReturnToolSupport.FindAsync(services, ReadString(arguments, "returnId"), ReadString(arguments, "returnNumber"), cancellationToken);

        if (orderReturn is null)
        {
            return error;
        }

        if (orderReturn.Status is not (OrderReturnStatus.Requested or OrderReturnStatus.Approved))
        {
            return $"Nothing was done: return {orderReturn.Number} is {ReturnToolSupport.NameOf(orderReturn.Status)}; only a requested or approved return can be received.";
        }

        var restockAll = ReadBool(arguments, "restockAll") == true;
        var lineIds = ReadStrings(arguments, "restockLineIds") ?? [];

        if (restockAll && lineIds.Count > 0)
        {
            return "Nothing was done: give restockLineIds or restockAll, not both.";
        }

        if (lineIds.FirstOrDefault(id => !orderReturn.Lines.Any(line => line.LineId == id)) is { } unknown)
        {
            return $"Nothing was done: return {orderReturn.Number} has no line '{unknown}'. Use getOrderReturn for its lines.";
        }

        var restock = restockAll ? orderReturn.Lines.Select(line => line.LineId).ToList() : lineIds;
        var locationId = ReadString(arguments, "locationId");

        if (locationId is not null)
        {
            var known = false;

            foreach (var provider in services.GetServices<IFulfillmentLocationProvider>())
            {
                known |= (await provider.GetLocationsAsync()).Any(location => string.Equals(location.LocationId, locationId, StringComparison.Ordinal));
            }

            if (!known)
            {
                return $"Nothing was done: '{locationId}' is not a stock location. Use getOrderReturn for the locations the items can go back to.";
            }
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var received = await services.GetRequiredService<OrderReturnService>().ReceiveAsync(
            orderReturn,
            restock,
            locationId,
            caller.FindFirstValue(ClaimTypes.NameIdentifier),
            GetActor(caller),
            cancellationToken);

        if (!received)
        {
            await services.GetRequiredService<YesSqlSession>().CancelAsync();

            if (services.GetService<OrdersToolScope>() is { } scope)
            {
                scope.SessionClosed = true;
            }

            return $"Nothing was done: return {orderReturn.Number} can't be received now; its order may be gone. Check the return's page.";
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            id = orderReturn.ItemId,
            number = orderReturn.Number,
            orderNumber = orderReturn.OrderNumber,
            status = ReturnToolSupport.NameOf(orderReturn.Status),
            restockedLines = orderReturn.Lines.Where(line => line.Restocked).Select(line => new { lineId = line.LineId, title = line.Title, quantity = line.Quantity }),
            restockedUnits = orderReturn.Lines.Where(line => line.Restocked).Sum(line => line.Quantity),
            locationId = orderReturn.RestockLocationId,
            message = $"Return {orderReturn.Number} received. Nothing was refunded: preview the refund with previewRefund and its returnId, then refund it with refundOrder.",
        });
    }
}
