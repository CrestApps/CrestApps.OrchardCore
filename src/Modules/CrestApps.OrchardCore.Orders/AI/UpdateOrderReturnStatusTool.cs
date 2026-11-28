using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Approves, declines or closes the exchange of a return through the steps of its page on Commerce › Returns
/// (<see cref="OrderReturnService.ApproveAsync"/>, <see cref="OrderReturnService.RejectAsync"/>,
/// <see cref="OrderReturnService.CompleteExchangeAsync"/>), each of which notes the order and emails the buyer. As on
/// the page, a step the return's status does not allow keeps nothing, and a step that worked is committed at once.
/// </summary>
internal sealed class UpdateOrderReturnStatusTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateOrderReturnStatus";

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
        "action": {
          "type": "string",
          "enum": ["approve", "decline", "exchanged"],
          "description": "approve a requested return (the buyer is emailed how to send the items back), decline a requested or approved one (a message is required), or exchanged to close an exchange once the replacement is on its way."
        },
        "message": {
          "type": "string",
          "description": "The message for the buyer, put in the email: how to send the items back, why the return is declined (required), or about the replacement."
        }
      },
      "required": ["action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Approves a return, declines it (a reason is required), or closes an exchange once the replacement is on its way. Each of these emails the buyer, with the message given.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var action = ReadString(arguments, "action")?.ToLowerInvariant();

        if (action is not ("approve" or "decline" or "exchanged"))
        {
            return "Nothing was done: action must be approve, decline or exchanged.";
        }

        var message = ReadString(arguments, "message");

        if (action == "decline" && message is null)
        {
            return "Nothing was done: tell the buyer why the return is declined, in message.";
        }

        var (orderReturn, error) = await ReturnToolSupport.FindAsync(services, ReadString(arguments, "returnId"), ReadString(arguments, "returnNumber"), cancellationToken);

        if (orderReturn is null)
        {
            return error;
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var returnService = services.GetRequiredService<OrderReturnService>();
        var actor = GetActor(caller);
        var before = ReturnToolSupport.NameOf(orderReturn.Status);

        var done = action switch
        {
            "approve" => await returnService.ApproveAsync(orderReturn, message, actor, cancellationToken),
            "decline" => await returnService.RejectAsync(orderReturn, message, actor, cancellationToken),
            _ => await returnService.CompleteExchangeAsync(orderReturn, message, actor, cancellationToken),
        };

        if (!done)
        {
            await services.GetRequiredService<YesSqlSession>().CancelAsync();
            MarkClosed(services);

            return action switch
            {
                "approve" => $"Nothing was done: return {orderReturn.Number} is {before}; only a requested return can be approved.",
                "decline" => $"Nothing was done: return {orderReturn.Number} is {before}; only a requested or approved return can be declined.",
                _ => $"Nothing was done: return {orderReturn.Number} is {before} and the buyer wants {ToCamel(orderReturn.Resolution.ToString())}; only an approved or received exchange can be closed as exchanged.",
            };
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
            previousStatus = before,
            status = ReturnToolSupport.NameOf(orderReturn.Status),
            message = action switch
            {
                "approve" => $"Return {orderReturn.Number} approved. The buyer is emailed how to send the items back when the site's email is set up.",
                "decline" => $"Return {orderReturn.Number} declined. The buyer is emailed why when the site's email is set up.",
                _ => $"Return {orderReturn.Number} closed as exchanged. The buyer is emailed when the site's email is set up.",
            },
        });
    }

    private static void MarkClosed(IServiceProvider services)
    {
        if (services.GetService<OrdersToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }
}
