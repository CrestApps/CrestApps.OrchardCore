using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Records money received outside the site against a transaction through the Record payment form of its page
/// (<see cref="ITransactionCommandService.RecordPaymentAsync"/>, which locks, checks and commits the transaction): the
/// amount, how it was paid, when it was received (entered in the site's time zone and stored in UTC, as the form does),
/// its reference and note, and whether money above what is owed is kept as the customer's credit. No money moves, but
/// the entry is permanent and can send the customer a receipt, so the tool follows the owner's money rules: the amount
/// and the transaction's currency are always given, paying the whole balance needs <c>entireBalance</c>, more than is
/// owed is refused unless <c>creditExcess</c> (with <c>entireBalance</c>) keeps the rest as credit, the reference and note
/// never hold a card or bank account number, and every call carries an idempotency key. The key is looked up before what
/// is owed is checked, so a retry of a payment that was recorded gets its first answer back, never a refusal about the
/// balance that payment lowered.
/// </summary>
internal sealed class RecordTransactionPaymentTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "recordTransactionPayment";

    /// <summary>
    /// The longest payment method text the Record payment form takes.
    /// </summary>
    public const int MaxPaymentMethodLength = 100;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions."
        },
        "amount": {
          "type": "number",
          "description": "The amount received, more than zero, in the transaction's currency. No more than what is still owed, unless creditExcess is true."
        },
        "currency": {
          "type": "string",
          "description": "The transaction's currency, such as USD. Another currency is refused."
        },
        "entireBalance": {
          "type": "boolean",
          "description": "Set to true when the payment covers everything still owed; amount must then be exactly that, or more with creditExcess. An amount equal to what is owed needs it."
        },
        "creditExcess": {
          "type": "boolean",
          "description": "Set to true only when the customer paid more than is owed and the rest is kept as their customer credit (needs the Customers Store Credit feature and a payer who can hold credit). It is the only way to record more than is owed; it needs entireBalance too."
        },
        "paymentMethod": {
          "type": "string",
          "description": "How the money was paid: cash, check, bank-transfer, card-terminal or other, or any short text up to 100 characters."
        },
        "receivedAt": {
          "type": "string",
          "description": "When the money was received, yyyy-MM-ddTHH:mm in the site's time zone, such as a check dated last week. Not in the future. Left out, it was received now."
        },
        "reference": {
          "type": "string",
          "description": "The payment's reference, such as a bank transfer or check number."
        },
        "note": {
          "type": "string",
          "description": "A note kept with the payment."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this payment, up to 128 characters, such as payment-inv42-wire8831. A second call with the same key is refused, so a retry never records the payment twice."
        }
      },
      "required": ["transactionId", "amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records money received outside the site against a transaction: an amount in its currency with how it was paid, when it was received, a reference and a note, or everything still owed with entireBalance. More than is owed is refused unless creditExcess keeps the rest as the customer's credit. No money moves, but the entry cannot be removed afterwards and it can send the customer a receipt. Every call needs a new idempotency key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadKey(arguments);

        if (key is null)
        {
            return $"Nothing was recorded: give a new idempotencyKey of up to {MaxKeyLength} characters for this payment.";
        }

        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        if (!TryReadDecimal(arguments, "amount", out var amount))
        {
            return "Nothing was recorded: amount must be a number.";
        }

        if (CheckAmount(transaction, amount, ReadString(arguments, "currency"), "recorded") is { } amountError)
        {
            return amountError;
        }

        var entireBalance = ReadBool(arguments, "entireBalance") == true;
        var creditExcess = ReadBool(arguments, "creditExcess") == true;

        // Keeping the excess as credit only makes sense for a payment that covers everything owed.
        if (creditExcess && !entireBalance)
        {
            return "Nothing was recorded: creditExcess keeps what was paid above everything still owed as the customer's credit, so it needs entireBalance set to true too.";
        }

        var paymentMethod = ReadString(arguments, "paymentMethod");

        if (paymentMethod is { Length: > MaxPaymentMethodLength })
        {
            return $"Nothing was recorded: paymentMethod can be at most {MaxPaymentMethodLength} characters.";
        }

        var receivedAt = ReadString(arguments, "receivedAt");
        DateTime? receivedUtc = null;

        if (receivedAt is not null)
        {
            // Entered in the site's time zone, as the Record payment form takes it, and stored in UTC.
            (receivedUtc, var receivedError) = await AILocalTimeArguments.ReadPastAsync(
                services.GetRequiredService<ILocalClock>(),
                services.GetRequiredService<IClock>(),
                receivedAt,
                "Nothing was recorded",
                "receivedAt",
                "the time the payment was received");

            if (receivedError is not null)
            {
                return receivedError;
            }
        }

        var reference = ReadString(arguments, "reference");
        var note = ReadString(arguments, "note");

        if (AIMoneyArguments.RefuseAccountNumbers("Nothing was recorded", reference, note) is { } numberError)
        {
            return numberError;
        }

        var fingerprint = string.Join('|',
            transaction.ItemId,
            Invariant(amount.Value),
            transaction.Currency,
            entireBalance,
            creditExcess,
            paymentMethod,
            receivedUtc?.ToString("O", CultureInfo.InvariantCulture),
            reference,
            note);

        // The key is looked up before what is owed is checked: a retry of a payment that was recorded gets its first answer
        // back, instead of a refusal about the balance the payment itself lowered.
        return await RunOnceAsync(services, caller, key, fingerprint, "recorded", async token =>
        {
            var outstanding = Outstanding(transaction);

            if (amount.Value > outstanding && !creditExcess)
            {
                return new IdempotentOperationAnswer($"Nothing was recorded: only {Format(outstanding, transaction.Currency)} is still owed on this transaction. To keep what was paid above that as the customer's credit, set creditExcess and entireBalance to true.", Keep: false);
            }

            if (entireBalance != (amount.Value >= outstanding))
            {
                return new IdempotentOperationAnswer(entireBalance
                    ? $"Nothing was recorded: everything still owed is {Format(outstanding, transaction.Currency)}. Give that amount with entireBalance, or a smaller amount without it."
                    : $"Nothing was recorded: {Format(amount.Value, transaction.Currency)} is everything still owed. Set entireBalance to true to record it as paid in full.", Keep: false);
            }

            TransactionCommandResult result;

            try
            {
                // As the Record payment form posts it: the service stores a common method by its key, whether its key or
                // its label was given, refuses a received time in the future, and keeps the excess as credit only when
                // asked and the payer can hold it.
                result = await services.GetRequiredService<ITransactionCommandService>().RecordPaymentAsync(transaction.ItemId, new TransactionPaymentRequest
                {
                    Amount = amount.Value,
                    PaymentMethod = paymentMethod,
                    ReceivedUtc = receivedUtc,
                    Reference = reference,
                    Note = note,
                    CreditExcess = creditExcess,
                }, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The payment may already be saved (the receipt handlers run after the save), so the key stays used.
                return new IdempotentOperationAnswer("Recording the payment stopped with an error, and it may already be recorded. Do not retry with this or another key; read the transaction with getTransaction.", Keep: true);
            }

            if (!result.Succeeded)
            {
                // Nothing was recorded, so the key may be used again.
                return new IdempotentOperationAnswer($"Nothing was recorded: {string.Join(" ", result.Errors)}", Keep: false);
            }

            var recorded = result.Event?.Amount ?? Math.Min(amount.Value, outstanding);
            var message = $"The payment of {Format(recorded, transaction.Currency)} was recorded. No money moved through the site.";

            if (result.CreditedAmount > 0m)
            {
                message += $" {Format(result.CreditedAmount, transaction.Currency)} above what was owed was added to the customer's credit.";
            }

            if (result.UnappliedAmount > 0m)
            {
                message += $" {Format(result.UnappliedAmount, transaction.Currency)} was more than was owed and was not recorded; give it back to the customer.";
            }

            var answer = await DescribeAsync(services, result.Transaction);
            answer["recorded"] = recorded;
            answer["paymentMethod"] = result.Event?.PaymentMethod ?? paymentMethod;
            answer["receivedAt"] = await LocalTimeAsync(services, result.Event?.ReceivedUtc ?? receivedUtc);
            answer["reference"] = reference;
            answer["creditedAmount"] = result.CreditedAmount > 0m ? CurrencyScale.Round(result.CreditedAmount, transaction.Currency) : null;
            answer["message"] = message;

            return new IdempotentOperationAnswer(Json(answer), Keep: true);
        }, cancellationToken);
    }
}
