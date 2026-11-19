using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Cancels an order as the Cancel form of its page does (<see cref="IOrderOperationsService.CancelAsync"/>), with a note
/// and optionally putting what a paid order sold back into stock. What is still owed for it through Pay Later is
/// canceled too, and an unpaid order's checkout is let go. Narrower than the page and the catalog row: it never refunds;
/// a cancellation never moves money as a side effect, so what was paid stays refundable and the result points to
/// <c>refundOrder</c>. It asks for approval.
/// </summary>
internal sealed class CancelOrderTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelOrder";

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
        "note": {
          "type": "string",
          "description": "Why it is canceled, kept on the order."
        },
        "restock": {
          "type": "boolean",
          "description": "For a paid order: put what it sold back into stock."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels an order with an optional note, optionally putting what a paid order sold back into stock; anything still owed for it is canceled too. It never refunds: what was paid stays refundable with refundOrder. A canceled order cannot be reopened.";

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

        if (order.Status == OrderStatus.Canceled)
        {
            return $"Nothing was changed: order {order.OrderNumber} is canceled already.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var operations = services.GetRequiredService<IOrderOperationsService>();
        var restock = ReadBool(arguments, "restock") == true;
        var result = await operations.CancelAsync(order, ReadString(arguments, "note"), refund: false, restock, GetActor(caller), cancellationToken);

        if (!result.Succeeded)
        {
            return $"The order was not canceled: {string.Join(" ", result.Errors)}";
        }

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        var money = await operations.GetMoneyAsync(order, cancellationToken);

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            status = "canceled",
            restocked = restock,
            messages = result.Messages,
            refundable = money.Refundable,
            currency = order.Currency,
            message = money.Refundable > 0m
                ? $"Order {order.OrderNumber} was canceled. Nothing was refunded: {Format(money.Refundable, order.Currency)} paid for it can still be refunded with refundOrder."
                : $"Order {order.OrderNumber} was canceled.",
        });
    }
}
