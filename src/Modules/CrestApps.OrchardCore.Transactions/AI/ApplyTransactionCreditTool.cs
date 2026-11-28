using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Pays toward a transaction with its payer's customer credit (store credit, which business accounts hold too) through
/// the Apply customer credit form of its page (<see cref="ITransactionCommandService.ApplyCreditAsync"/>, which locks,
/// checks and commits, then spends the credit). It spends the customer's money and is recorded like any other payment,
/// so it follows the owner's money rules (F23): the amount and the transaction's currency are given, no more than what is
/// owed nor than the credit (the command would quietly cap it), everything owed needs <c>entireBalance</c>, the call
/// carries the token <c>previewTransactionAction</c> issued for the same payment and a new idempotency key, and the note
/// never holds a card or bank account number.
/// </summary>
/// <remarks>
/// The idempotency key is looked up first, so a retry of a payment that went through gets its first answer back instead
/// of a refusal about the balance that has since changed; the checks and the token are taken inside the operation, and a
/// refusal keeps the key free.
/// </remarks>
internal sealed class ApplyTransactionCreditTool : TransactionMoneyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "applyTransactionCredit";

    private const string Refused = "Nothing was applied";

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
          "description": "The credit to apply, more than zero, in the transaction's currency: no more than what is still owed nor than the customer's credit (getTransaction's payer.creditBalance)."
        },
        "currency": {
          "type": "string",
          "description": "The transaction's currency, such as USD."
        },
        "entireBalance": {
          "type": "boolean",
          "description": "Set to true when the amount is everything still owed; amount must then be exactly that."
        },
        "note": {
          "type": "string",
          "description": "A note kept with the payment, up to 500 characters. Never a card or bank account number."
        },
        "previewToken": {
          "type": "string",
          "description": "The previewToken previewTransactionAction returned for this transaction with the applyCredit action and the same amount, currency, entireBalance and note. It works once and expires 10 minutes after the preview."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this payment, up to 128 characters, such as credit-inv42-1. A second call with the same key returns the first answer, so a retry never spends the credit twice."
        }
      },
      "required": ["transactionId", "amount", "currency", "previewToken", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Pays toward an outstanding transaction with its payer's customer credit (store credit, also held by business accounts), recorded like any other payment, so the customer may be e-mailed a receipt. The amount is in the transaction's currency, no more than what is owed and no more than the credit; everything owed needs entireBalance. Call previewTransactionAction with the applyCredit action first and show the result to the user: this tool needs the previewToken it returns and a new idempotencyKey.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadKey(arguments);

        if (key is null)
        {
            return $"{Refused}: give a new idempotencyKey of up to {MaxKeyLength} characters for this payment.";
        }

        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        var previewToken = ReadString(arguments, "previewToken");

        return await RunOnceAsync(services, caller, key, RequestFingerprint(ApplyCreditAction, transaction, arguments), "applied", async token =>
        {
            var (request, refusal) = await ReadActionAsync(services, arguments, transaction, ApplyCreditAction, "applied", token);

            if (request is null)
            {
                return new IdempotentOperationAnswer(refusal, Keep: false);
            }

            if (await TakePreviewTokenAsync(services, caller, previewToken, request.Fingerprint, Refused, token) is { } tokenError)
            {
                return new IdempotentOperationAnswer(tokenError, Keep: false);
            }

            TransactionCommandResult result;

            try
            {
                result = await services.GetRequiredService<ITransactionCommandService>().ApplyCreditAsync(transaction.ItemId, request.Amount, request.Note, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The payment may already be saved (the credit is spent after the save), so the key stays used.
                return new IdempotentOperationAnswer("Applying the credit stopped with an error, and it may already be applied. Do not retry with this or another key; read the transaction with getTransaction.", Keep: true);
            }

            if (!result.Succeeded)
            {
                return new IdempotentOperationAnswer($"{Refused}: {string.Join(" ", result.Errors)}", Keep: false);
            }

            var current = result.Transaction ?? transaction;
            var applied = result.Event?.Amount ?? request.Amount;
            decimal? balance = null;

            if (services.GetService<ICustomerCreditService>() is { } credit && current.GetPayer() is { IsEmpty: false } payer)
            {
                balance = CurrencyScale.Round(await credit.GetBalanceAsync(payer, current.Currency, token), current.Currency);
            }

            var answer = await DescribeAsync(services, current);
            answer["applied"] = applied;
            answer["creditBalance"] = balance;
            answer["warnings"] = result.Warnings.Count > 0 ? result.Warnings.ToList() : null;
            answer["adminPath"] = GetAdminPath(services, "TransactionsDetail", new { action = "Detail", itemId = current.ItemId });
            answer["message"] = result.Warnings.Count > 0
                ? $"{Format(applied, current.Currency)} of the customer's credit was recorded as a payment, but read the warnings: the credit may have been taken back."
                : $"{Format(applied, current.Currency)} of the customer's credit was applied as a payment.";

            return new IdempotentOperationAnswer(Json(answer), Keep: true);
        }, cancellationToken);
    }
}
