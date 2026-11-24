using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Returns one return request as its page on Commerce › Returns shows it: the lines and reasons, the buyer's comment,
/// what the order can still refund and the suggested refund (the returned units' price, capped at what is refundable,
/// <see cref="OrderReturnPolicy.GetItemsValue"/>), the return instructions and the stock locations the items can go
/// back to.
/// </summary>
internal sealed class GetOrderReturnTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getOrderReturn";

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
          "description": "The return's number, such as RMA-261019-K3QZ7, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one return request with its lines and reasons, the buyer's comment, its order, what the order can still refund and the suggested refund for the returned items. Use it before deciding, receiving or refunding a return.";

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

        var order = await services.GetRequiredService<IOrderStore>().FindByIdAsync(orderReturn.OrderId, cancellationToken);
        var refundable = order is null ? 0m : (await services.GetRequiredService<IOrderOperationsService>().GetMoneyAsync(order, cancellationToken)).Refundable;
        var locations = new List<object>();

        foreach (var provider in services.GetServices<IFulfillmentLocationProvider>())
        {
            locations.AddRange((await provider.GetLocationsAsync()).Select(location => new { id = location.LocationId, name = location.Name }));
        }

        var settings = services.GetService<ISiteService>() is { } siteService
            ? await siteService.GetSettingsAsync<ReturnSettings>()
            : null;

        return Json(new
        {
            id = orderReturn.ItemId,
            number = orderReturn.Number,
            orderId = orderReturn.OrderId,
            orderNumber = orderReturn.OrderNumber,
            customerName = orderReturn.CustomerName,
            customerEmail = orderReturn.CustomerEmail,
            status = ReturnToolSupport.NameOf(orderReturn.Status),
            wants = ToCamel(orderReturn.Resolution.ToString()),
            exchangeFor = orderReturn.ExchangeFor,
            comment = orderReturn.Comment,
            storeMessage = orderReturn.StoreMessage,
            currency = orderReturn.Currency,
            lines = orderReturn.Lines.Select(line => new
            {
                lineId = line.LineId,
                title = line.Title,
                sku = line.Sku,
                quantity = line.Quantity,
                unitPrice = line.UnitPrice,
                reason = line.Reason,
                restocked = line.Restocked,
            }),
            refundedAmount = orderReturn.RefundedAmount,
            orderRefundable = refundable,
            suggestedRefund = Math.Min(refundable, OrderReturnPolicy.GetItemsValue(orderReturn)),
            restockLocationId = orderReturn.RestockLocationId,
            restockLocations = locations,
            returnInstructions = settings?.Instructions,
            requestedAt = await LocalTimeAsync(services, orderReturn.CreatedUtc),
            approvedAt = await LocalTimeAsync(services, orderReturn.ApprovedUtc),
            receivedAt = await LocalTimeAsync(services, orderReturn.ReceivedUtc),
            closedAt = await LocalTimeAsync(services, orderReturn.ClosedUtc),
            canDecide = orderReturn.Status is OrderReturnStatus.Requested or OrderReturnStatus.Approved,
            canReceive = orderReturn.Status is OrderReturnStatus.Requested or OrderReturnStatus.Approved,
            canRefund = orderReturn.Status is OrderReturnStatus.Approved or OrderReturnStatus.Received,
            adminPath = GetAdminPath(services, "ReturnsDetail", new { returnId = orderReturn.ItemId }),
        });
    }
}
