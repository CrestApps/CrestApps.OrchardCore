using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Gives money back for an order through the refund the order's page uses
/// (<see cref="IOrderOperationsService.RefundAsync(Order, decimal?, string, bool, string, bool, CancellationToken)"/>):
/// online payments through the checkout refund service (<c>ICheckoutRefundService</c>) and the payment provider, newest
/// first, then Pay Later and offline payments, then what a gift card or store credit paid; or, when asked, as store
/// credit. It applies the page's limits (never more than is still refundable) and relays the service's messages and
/// errors as they are, which explain partial, pending and by-hand refunds.
/// </summary>
/// <remarks>
/// Stricter than the page, as the owner asked (F23): the amount and its currency are always given, the currency must be
/// the order's and the amount must fit its decimal places; refunding everything still refundable needs <c>refundAll</c>
/// with that amount (the rules of <see cref="OrderRefundAIToolBase.ReadRefundAsync"/>, which <c>previewRefund</c> shares);
/// every call carries the token <c>previewRefund</c> issued for the same caller and the same refund, taken once and only
/// before it expires (<see cref="AIActionPreviewTokens"/>); and every call carries an idempotency key, kept for a day per
/// site and caller (<see cref="IdempotentOperation"/>), so a repeated call with the same key is refused instead of
/// refunding twice. The token is taken inside the idempotent operation, so a retry with the same key gets the first
/// answer back without a new token, and a refused token keeps the key free. A refund that moved money is committed
/// before the answer. Every call that ran keeps its key, even one the service refused or one that failed with an error or
/// at the commit, because the provider may already have given the money back.
/// </remarks>
internal sealed class RefundOrderTool : OrderRefundAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "refundOrder";

    private const int MaxKeyLength = 128;

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);

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
        "amount": {
          "type": "number",
          "description": "How much to give back, more than zero, in the order's currency. With refundAll it must be everything still refundable, from getOrder."
        },
        "currency": {
          "type": "string",
          "description": "The order's currency, such as USD. A refund in another currency is refused."
        },
        "refundAll": {
          "type": "boolean",
          "description": "Set to true to give back everything still refundable; amount must then be exactly that. An amount equal to everything refundable needs it."
        },
        "reason": {
          "type": "string",
          "description": "Why the money is given back, kept with the refund."
        },
        "restock": {
          "type": "boolean",
          "description": "Put what the order sold back into stock once everything is refunded."
        },
        "asStoreCredit": {
          "type": "boolean",
          "description": "Give the amount as store credit for the buyer instead of back to how they paid (only when getOrder says canRefundAsStoreCredit)."
        },
        "previewToken": {
          "type": "string",
          "description": "The previewToken previewRefund returned for this same order, amount, currency and options. It works once and expires 10 minutes after the preview; call previewRefund again for a new one."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this refund, up to 128 characters, such as refund-1042-damaged-box. A second call with the same key is refused, so a retry never refunds twice."
        },
        "returnId": {
          "type": "string",
          "description": "The id of an approved or received return of this order, from listOrderReturns, when the money is given back for that return: the refund then closes the return and emails the buyer. It cannot be combined with restock (receiveOrderReturn puts returned items back) or asStoreCredit."
        }
      },
      "required": ["amount", "currency", "previewToken", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gives money back for an order: a stated amount and currency, or everything still refundable with refundAll, newest payment first through the payment provider, optionally putting the items back into stock or giving the amount as store credit instead. Call previewRefund first and show its result to the user: this tool needs the previewToken it returns, for the same order, amount, currency and options, and a new idempotency key; a used or expired token and a repeated key are refused. A refund cannot be undone; a payment method that cannot refund automatically is recorded for settling by hand. For a buyer's return, give its returnId: the refund then closes the return and emails the buyer.";

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

        var key = ReadString(arguments, "idempotencyKey");

        if (key is null || key.Length > MaxKeyLength)
        {
            return $"Nothing was refunded: give a new idempotencyKey of up to {MaxKeyLength} characters for this refund.";
        }

        var (request, refusal) = await ReadRefundAsync(services, arguments, order, "Nothing was refunded", cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        var previewToken = ReadString(arguments, "previewToken");

        if (previewToken is null)
        {
            return "Nothing was refunded: call previewRefund first with the same order, amount, currency and options, show its result to the user, and pass the previewToken it returns. A token works once and expires 10 minutes after the preview.";
        }

        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (cache is null || locks is null)
        {
            return "Nothing was refunded: the site can't keep idempotency keys right now, so a refund from a chat can't be guarded against running twice. Refund it from the order's page.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var reason = ReadString(arguments, "reason");
        var amount = request.Amount;
        var asStoreCredit = request.AsStoreCredit;
        var operations = services.GetRequiredService<IOrderOperationsService>();
        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{TheName}|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";

        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, request.RequestFingerprint, _keyLifetime, async token =>
        {
            // The preview's token is taken only when the refund runs: a retry with the same key is answered without it,
            // and a refused token is not kept, so the key stays free for a call with a valid token.
            var utcNow = services.GetRequiredService<IClock>().UtcNow;
            var taken = await AIActionPreviewTokens.TakeAsync(cache, locks, PreviewPurpose, GetPreviewScope(services, caller), previewToken, request.Fingerprint, utcNow, token);

            if (taken != AIActionPreviewTokenResult.Taken)
            {
                return new IdempotentOperationAnswer(DescribeRefusedToken(taken, order), Keep: false);
            }

            if (request.Return is not null)
            {
                return await RefundReturnAsync(services, request.ReturnService, order, request.Return, amount, GetActor(caller), token);
            }

            OrderOperationResult result;

            try
            {
                result = await operations.RefundAsync(order, amount, reason, request.Restock, GetActor(caller), asStoreCredit, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Part of the refund may have reached the provider; the key is used up so a retry can't refund twice.
                await services.GetRequiredService<YesSqlSession>().CancelAsync();
                CloseSession(services);

                return new IdempotentOperationAnswer(
                    $"The refund of order {order.OrderNumber} stopped with an error, and whether money was given back is unknown. Do not retry with this or another key; check the order's page and the Refunds page.",
                    Keep: true);
            }
            var movedMoney = result.Refunded > 0m || result.Messages.Count > 0;
            var saved = true;

            if (movedMoney)
            {
                saved = await SaveAsync(services, token);
            }

            var answer = Json(new
            {
                orderId = order.ItemId,
                number = order.OrderNumber,
                asked = amount,
                refunded = result.Refunded,
                currency = order.Currency,
                asStoreCredit,
                messages = result.Messages,
                errors = result.Errors,
                recordSaved = movedMoney ? saved : (bool?)null,
                message = !saved
                    ? $"The payment provider may already have given back {Format(result.Refunded, order.Currency)}, but the order's record of the refund was not saved because the order changed at the same moment. Do not retry; check the order's page and the Refunds page."
                    : result.Refunded > 0m
                        ? $"Refunded {Format(result.Refunded, order.Currency)} on order {order.OrderNumber}."
                        : result.Messages.Count > 0
                            ? "The refund was recorded; read the messages for what happens next."
                            : "Nothing was refunded; read the errors. This previewToken and idempotency key are used up: to try again, call previewRefund for a new token and use a new key.",
            });

            // Every answer is kept, so the key is refused from then on, whatever the refund came to.
            return new IdempotentOperationAnswer(answer, Keep: true);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was refunded again: the idempotency key '{key}' was already used for this refund. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was refunded: the idempotency key '{key}' was already used for a different refund. Use a new key for a new refund.",
            _ => $"Nothing was refunded: a refund with the idempotency key '{key}' is still running. Read the order again with getOrder in your next message.",
        };
    }

    private static string DescribeRefusedToken(AIActionPreviewTokenResult result, Order order)
        => result switch
        {
            AIActionPreviewTokenResult.Mismatch => $"Nothing was refunded: the previewToken was issued for another refund, or the order's refunds or payments changed since the preview. Call previewRefund again for this refund of order {order.OrderNumber}, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Expired => $"Nothing was refunded: the previewToken expired. Call previewRefund again for order {order.OrderNumber}, show the result to the user, and use the new token.",
            AIActionPreviewTokenResult.Busy => "Nothing was refunded: another refund is using the same previewToken at this moment. Read the order again with getOrder in your next message.",
            _ => $"Nothing was refunded: the previewToken is not one previewRefund issued to you, or it was already used or has expired. Call previewRefund again for order {order.OrderNumber}, show the result to the user, and use the new token.",
        };

    // Refunds a return as the Returns page does (OrderReturnService.RefundAsync: refund the order, close the return,
    // email the buyer). The service closes the return even when the payment method records the refund for settling by
    // hand, so the answer gives what the return's refunded amount grew by, never that the money was paid back.
    private static async Task<IdempotentOperationAnswer> RefundReturnAsync(
        IServiceProvider services,
        OrderReturnService returnService,
        Order order,
        OrderReturn orderReturn,
        decimal amount,
        string actor,
        CancellationToken cancellationToken)
    {
        var before = orderReturn.RefundedAmount;
        IReadOnlyList<string> problems;

        try
        {
            problems = await returnService.RefundAsync(orderReturn, amount, actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await services.GetRequiredService<YesSqlSession>().CancelAsync();
            CloseSession(services);

            return new IdempotentOperationAnswer(
                $"The refund of return {orderReturn.Number} stopped with an error, and whether money was given back is unknown. Do not retry with this or another key; check the return's page and the Refunds page.",
                Keep: true);
        }

        if (problems.Count > 0)
        {
            // As the Returns page: a refused step keeps nothing of it.
            await services.GetRequiredService<YesSqlSession>().CancelAsync();
            CloseSession(services);

            return new IdempotentOperationAnswer(Json(new
            {
                orderId = order.ItemId,
                number = order.OrderNumber,
                returnId = orderReturn.ItemId,
                returnNumber = orderReturn.Number,
                errors = problems,
                message = "The return was not refunded or closed; read the errors. Check the Refunds page before trying again, with a new previewToken from previewRefund and a new key.",
            }), Keep: true);
        }

        var saved = await SaveAsync(services, cancellationToken);
        var recorded = orderReturn.RefundedAmount - before;

        return new IdempotentOperationAnswer(Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            returnId = orderReturn.ItemId,
            returnNumber = orderReturn.Number,
            asked = amount,
            recordedForReturn = recorded,
            currency = order.Currency,
            returnStatus = saved ? ToCamel(orderReturn.Status.ToString()) : null,
            recordSaved = saved,
            message = saved
                ? $"Return {orderReturn.Number} was closed with {Format(recorded, order.Currency)} recorded as refunded; the buyer is emailed when the site's email is set up. Read the order with getOrder, or the Refunds page, to see whether the payment method paid it back or it waits to be settled by hand."
                : $"The payment provider may already have given money back for return {orderReturn.Number}, but the record was not saved because the order or return changed at the same moment. Do not retry; check the return's page and the Refunds page.",
        }), Keep: true);
    }

    // Commits the refund's record at once. When the commit fails, such as when the order changed at the same moment,
    // the record is not saved, though the provider may have refunded.
    private static async Task<bool> SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<YesSqlSession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await session.CancelAsync();
            CloseSession(services);

            return false;
        }
    }

    private static void CloseSession(IServiceProvider services)
    {
        if (services.GetService<OrdersToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }
}
