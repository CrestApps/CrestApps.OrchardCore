using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Adds an internal note to an order's history under the caller's name, as the note box of the order's page does
/// (<see cref="IOrderService.AddNoteAsync"/> with <see cref="OrderNoteKind.Note"/>). The buyer does not see it.
/// </summary>
internal sealed class AddOrderNoteTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "addOrderNote";

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
        "text": {
          "type": "string",
          "description": "The note. Only staff see it."
        }
      },
      "required": ["text"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds an internal note to an order's history under the current user's name. The buyer does not see it.";

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

        if (ReadString(arguments, "text") is not { } text)
        {
            return "Nothing was added: write the note first.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        await services.GetRequiredService<IOrderService>().AddNoteAsync(order, text, OrderNoteKind.Note, GetActor(caller), cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            note = text,
            by = GetActor(caller),
            message = $"The note was added to order {order.OrderNumber}. The buyer does not see it.",
        });
    }
}
