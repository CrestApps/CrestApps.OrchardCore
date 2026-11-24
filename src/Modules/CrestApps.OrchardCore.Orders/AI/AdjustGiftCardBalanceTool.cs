using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Adds to a gift card's balance or takes from it, with a reason kept in its activity, through the Adjust action of the
/// card's page (<see cref="GiftCardService.AdjustAsync"/>, which holds the card's checkout lock and commits). The
/// amount and the card's currency are always given, the amount must fit the currency's decimal places, no more than the
/// balance can be taken, and every call carries an idempotency key (<see cref="IdempotentOperation"/>) so a retry
/// never changes the balance twice.
/// </summary>
internal sealed class AdjustGiftCardBalanceTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "adjustGiftCardBalance";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "giftCardId": {
          "type": "string",
          "description": "The card's id, from listGiftCards."
        },
        "amount": {
          "type": "number",
          "description": "How much to add, or a negative amount to take off; not zero, and never more than the balance taken off."
        },
        "currency": {
          "type": "string",
          "description": "The card's currency, such as USD. Another currency is refused."
        },
        "reason": {
          "type": "string",
          "description": "Why the balance changes, kept in the card's activity."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this change, up to 128 characters. A second call with the same key is refused, so a retry never changes the balance twice."
        }
      },
      "required": ["giftCardId", "amount", "currency", "reason", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds to a gift card's balance, or takes from it with a negative amount, in the card's currency, with a required reason that is kept in the card's activity. It is refused while the card is being used at a checkout. Every call needs a new idempotency key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = CheckKey(ReadString(arguments, "idempotencyKey"));

        if (key is null)
        {
            return $"Nothing was changed: give a new idempotencyKey of up to {MaxKeyLength} characters for this change.";
        }

        var (card, error) = await FindCardAsync(services, ReadString(arguments, "giftCardId"), cancellationToken);

        if (card is null)
        {
            return error;
        }

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is null or 0m)
        {
            return "Nothing was changed: give an amount to add, or a negative amount to take off.";
        }

        if (!string.Equals(ReadString(arguments, "currency"), card.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was changed: the card is in {card.Currency}; give the amount in {card.Currency}.";
        }

        if (Money.Round(amount.Value, card.Currency) != amount.Value)
        {
            return $"Nothing was changed: {Invariant(amount.Value)} has more decimal places than {card.Currency} allows.";
        }

        if (amount.Value < 0m && -amount.Value > card.Balance)
        {
            return $"Nothing was changed: the card's balance is only {Format(card.Balance, card.Currency)}.";
        }

        var reason = ReadString(arguments, "reason");

        if (reason is null)
        {
            return "Nothing was changed: say why the balance changes. It's kept with the card's activity.";
        }

        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (cache is null || locks is null)
        {
            return "Nothing was changed: the site can't keep idempotency keys right now, so a balance change from a chat can't be guarded against running twice. Change it on the card's page.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var before = card.Balance;
        var fingerprint = string.Join('|', card.ItemId, Invariant(amount.Value), card.Currency, reason);

        var outcome = await IdempotentOperation.RunAsync(cache, locks, KeyScope(services, TheName, caller), key, fingerprint, KeyLifetime, async token =>
        {
            GiftCard adjusted;

            try
            {
                adjusted = await services.GetRequiredService<GiftCardService>().AdjustAsync(card.ItemId, amount.Value, reason, GetActor(caller), token);
            }
            catch (ConcurrencyException)
            {
                await services.GetRequiredService<YesSqlSession>().CancelAsync();

                return new IdempotentOperationAnswer("Nothing was changed: the card changed at the same moment, such as at a checkout. Read it again with getGiftCard and retry in your next message with a new key.", Keep: false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Whether the balance was saved is unknown, so the key stays used.
                await services.GetRequiredService<YesSqlSession>().CancelAsync();

                return new IdempotentOperationAnswer("Changing the balance stopped with an error, and whether it changed is unknown. Do not retry with this or another key; read the card with getGiftCard.", Keep: true);
            }

            if (adjusted is null)
            {
                // The card is held by a checkout: nothing changed, so the key may be used again.
                return new IdempotentOperationAnswer("Nothing was changed: the card is being used at a checkout right now. Try again in a moment.", Keep: false);
            }

            var result = await DescribeAsync(services, adjusted);
            result["previousBalance"] = before;
            result["changedBy"] = adjusted.Balance - before;
            result["reason"] = reason;
            result["message"] = $"The balance is now {Format(adjusted.Balance, adjusted.Currency)}.";

            return new IdempotentOperationAnswer(Json(result), Keep: true);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was changed again: the idempotency key '{key}' was already used for this change. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was changed: the idempotency key '{key}' was already used for a different change. Use a new key for a new change.",
            _ => $"Nothing was changed: a change with the idempotency key '{key}' is still running. Read the card again with getGiftCard in your next message.",
        };
    }
}
