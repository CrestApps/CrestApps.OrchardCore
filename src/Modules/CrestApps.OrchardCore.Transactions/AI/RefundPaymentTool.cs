using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Refunds part or all of one settled payment that does not belong to an order, through the refund form of
/// Commerce › Payments (<see cref="ICheckoutRefundService.RequestRefundAsync"/> and the payment provider), with the
/// form's guards: only a payment the form would open, no more than is still refundable, and none while another refund
/// of it waits to be settled by hand. A payment of an order is refused, so an order is refunded only through
/// <c>refundOrder</c> and its rules. The owner's money rules apply: the amount and the payment's currency are always
/// given, refunding everything needs <c>refundAll</c>, and every call carries an idempotency key.
/// </summary>
internal sealed class RefundPaymentTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "refundPayment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "paymentId": {
          "type": "string",
          "description": "The payment's id, from listPayments. A payment that belongs to an order is refused: use refundOrder."
        },
        "amount": {
          "type": "number",
          "description": "How much to give back, more than zero, in the payment's currency. With refundAll it must be everything still refundable, from listPayments."
        },
        "currency": {
          "type": "string",
          "description": "The payment's currency, such as USD. Another currency is refused."
        },
        "refundAll": {
          "type": "boolean",
          "description": "Set to true to give back everything still refundable on the payment; amount must then be exactly that. An amount equal to it needs it."
        },
        "reason": {
          "type": "string",
          "description": "Why the money is given back, kept with the refund."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this refund, up to 128 characters. A second call with the same key is refused, so a retry never refunds twice."
        }
      },
      "required": ["paymentId", "amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Refunds an amount of one settled payment that is not an order's (such as a subscription or booking payment) through its payment provider, with a reason; everything still refundable needs refundAll. Payments of orders are refused: use refundOrder. The amount cannot exceed what is still refundable, the refund cannot be undone, and a method that cannot refund automatically is recorded for settling by hand. Every call needs a new idempotency key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadKey(arguments);

        if (key is null)
        {
            return $"Nothing was refunded: give a new idempotencyKey of up to {MaxKeyLength} characters for this refund.";
        }

        var paymentId = ReadString(arguments, "paymentId");
        var attempt = paymentId is null ? null : await services.GetRequiredService<IPaymentAttemptStore>().FindByIdAsync(paymentId, cancellationToken);

        if (attempt is null)
        {
            return $"Nothing was refunded: no payment has the id '{paymentId}'. Use listPayments to find it.";
        }

        if (string.Equals(attempt.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal))
        {
            return $"Nothing was refunded: this payment belongs to an order (id '{attempt.ReferenceId}'). Refund it with refundOrder, which keeps the order's refund rules and record.";
        }

        if (!PaymentAttemptRefundPolicy.CanRefund(attempt, services.GetRequiredService<TransactionDisplayText>()))
        {
            return "Nothing was refunded: only a settled payment made through a payment provider can be refunded here.";
        }

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is not > 0m)
        {
            return "Nothing was refunded: give the amount to give back, more than zero.";
        }

        if (!string.Equals(ReadString(arguments, "currency"), attempt.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was refunded: the payment was made in {attempt.Currency}; give the amount in {attempt.Currency}.";
        }

        if (CurrencyScale.Round(amount.Value, attempt.Currency) != amount.Value)
        {
            return $"Nothing was refunded: {Invariant(amount.Value)} has more decimal places than {attempt.Currency} allows.";
        }

        var refundStore = services.GetRequiredService<IPaymentRefundStore>();
        var (refunded, refunds) = await GetRefundedAsync(refundStore, attempt, cancellationToken);
        var remaining = CurrencyScale.Round(Math.Max(0m, Settled(attempt) - refunded), attempt.Currency);
        var refundAll = ReadBool(arguments, "refundAll") == true;

        if (refunds.Any(existing => existing.Status == RefundStatus.PendingManualReview))
        {
            return "Nothing was refunded: a refund for this payment is waiting to be settled by hand. Settle it with resolveRefund before starting another.";
        }

        if (amount.Value > remaining)
        {
            return $"Nothing was refunded: only {Format(remaining, attempt.Currency)} is still refundable on this payment.";
        }

        if (refundAll != (amount.Value == remaining))
        {
            return refundAll
                ? $"Nothing was refunded: everything still refundable is {Format(remaining, attempt.Currency)}. Give that amount with refundAll, or a smaller amount without it."
                : $"Nothing was refunded: {Format(amount.Value, attempt.Currency)} is everything still refundable. Set refundAll to true to give it all back.";
        }

        var reason = ReadString(arguments, "reason");
        var fingerprint = string.Join('|', attempt.ItemId, Invariant(amount.Value), attempt.Currency, refundAll);

        return await RunOnceAsync(services, caller, key, fingerprint, "refunded", async token =>
        {
            PaymentRefund refund;

            try
            {
                refund = await services.GetRequiredService<ICheckoutRefundService>().RequestRefundAsync(new RequestPaymentRefundContext
                {
                    SessionId = attempt.SessionId,
                    OriginalTransactionId = attempt.TransactionId,
                    Amount = amount.Value,
                    Reason = reason,
                }, token);
            }
            catch (InvalidOperationException)
            {
                // The service refused before starting the refund (another refund holds the lock, or the payment
                // changed), so nothing moved and the key may be used again.
                return new IdempotentOperationAnswer("Nothing was refunded: the refund couldn't be started. Another refund for this payment may still be in progress, or the payment changed. Read it again with listPayments and retry in your next message.", Keep: false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await services.GetRequiredService<ISession>().CancelAsync();

                return new IdempotentOperationAnswer("The refund stopped with an error, and whether money was given back is unknown. Do not retry with this or another key; check the Refunds page.", Keep: true);
            }

            var saved = true;

            try
            {
                await services.GetRequiredService<ISession>().SaveChangesAsync(token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await services.GetRequiredService<ISession>().CancelAsync();
                saved = false;
            }

            return new IdempotentOperationAnswer(Json(new
            {
                paymentId = attempt.ItemId,
                refundId = refund.ItemId,
                asked = amount.Value,
                currency = attempt.Currency,
                status = ToCamel(refund.Status.ToString()),
                failureReason = refund.FailureReason,
                recordSaved = saved,
                message = !saved
                    ? "The payment provider may already have given the money back, but the refund's record was not saved. Do not retry; check the Refunds page."
                    : refund.Status switch
                    {
                        RefundStatus.Succeeded => $"Refunded {Format(refund.RefundGrossAmount, attempt.Currency)}.",
                        RefundStatus.Failed or RefundStatus.Canceled => $"The refund failed: {refund.FailureReason ?? "the provider did not say why."}",
                        RefundStatus.PendingManualReview => "This payment method cannot refund automatically. The refund was recorded for settling by hand: pay the customer back, then use resolveRefund.",
                        _ => "The refund was submitted and is still being processed by the payment provider.",
                    },
            }), Keep: true);
        }, cancellationToken);
    }
}
