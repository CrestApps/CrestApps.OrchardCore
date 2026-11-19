using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using YesSql;
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
/// Stricter than the page, as the owner asked: the amount and its currency are always given, the currency must be the
/// order's and the amount must fit its decimal places; refunding everything still refundable needs <c>refundAll</c> with
/// that amount; and every call carries an idempotency key, kept for a day per site and caller
/// (<see cref="IdempotentOperation"/>), so a repeated call with the same key is refused instead of refunding twice. A
/// refund that moved money is committed before the answer, and its key is kept even when the commit fails, because the
/// provider may already have given the money back.
/// </remarks>
internal sealed class RefundOrderTool : OrdersAIToolBase
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
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this refund, up to 128 characters, such as refund-1042-damaged-box. A second call with the same key is refused, so a retry never refunds twice."
        }
      },
      "required": ["amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gives money back for an order: a stated amount and currency, or everything still refundable with refundAll, newest payment first through the payment provider, optionally putting the items back into stock or giving the amount as store credit instead. Every call needs a new idempotency key; a repeated key is refused. A refund cannot be undone; a payment method that cannot refund automatically is recorded for settling by hand.";

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

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is not > 0m)
        {
            return "Nothing was refunded: give the amount to give back, more than zero.";
        }

        var currency = ReadString(arguments, "currency");

        if (!string.Equals(currency, order.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was refunded: order {order.OrderNumber} was paid in {order.Currency}; give the amount in {order.Currency}.";
        }

        if (Money.Round(amount.Value, order.Currency) != amount.Value)
        {
            return $"Nothing was refunded: {amount.Value.ToString(CultureInfo.InvariantCulture)} has more decimal places than {order.Currency} allows.";
        }

        var refundAll = ReadBool(arguments, "refundAll") == true;
        var asStoreCredit = ReadBool(arguments, "asStoreCredit") == true;
        var restock = ReadBool(arguments, "restock") == true;
        var operations = services.GetRequiredService<IOrderOperationsService>();
        var money = await operations.GetMoneyAsync(order, cancellationToken);

        if (money.Refundable <= 0m)
        {
            return $"Nothing was refunded: nothing paid for order {order.OrderNumber} can be refunded.";
        }

        if (refundAll && amount.Value != money.Refundable)
        {
            return $"Nothing was refunded: everything still refundable for order {order.OrderNumber} is {Format(money.Refundable, order.Currency)}. Give that amount with refundAll, or a smaller amount without it.";
        }

        if (!refundAll && amount.Value >= money.Refundable)
        {
            return amount.Value == money.Refundable
                ? $"Nothing was refunded: {Format(amount.Value, order.Currency)} is everything still refundable for order {order.OrderNumber}. Set refundAll to true to give it all back."
                : $"Nothing was refunded: only {Format(money.Refundable, order.Currency)} can still be refunded for order {order.OrderNumber}.";
        }

        if (asStoreCredit && !money.CanRefundAsStoreCredit)
        {
            return "Nothing was refunded: this order's refund can't be given as store credit. The buyer needs an account and the Store credit feature must be on.";
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
        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{TheName}|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";
        var fingerprint = string.Join('|', order.ItemId, amount.Value.ToString(CultureInfo.InvariantCulture), order.Currency, refundAll, asStoreCredit, restock);

        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, fingerprint, _keyLifetime, async token =>
        {
            var result = await operations.RefundAsync(order, amount.Value, reason, restock, GetActor(caller), asStoreCredit, token);
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
                asked = amount.Value,
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
                            : "Nothing was refunded; read the errors.",
            });

            // An answer is kept, and the key refused from then on, once any money may have moved.
            return new IdempotentOperationAnswer(answer, Keep: movedMoney);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was refunded again: the idempotency key '{key}' was already used for this refund. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was refunded: the idempotency key '{key}' was already used for a different refund. Use a new key for a new refund.",
            _ => $"Nothing was refunded: a refund with the idempotency key '{key}' is still running. Read the order again with getOrder in your next message.",
        };
    }

    // Commits the refund's record at once. When the order changed at the same moment, the record is not saved, though the
    // provider may have refunded.
    private static async Task<bool> SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<YesSqlSession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            if (services.GetService<OrdersToolScope>() is { } scope)
            {
                scope.SessionClosed = true;
            }

            return false;
        }
    }
}
