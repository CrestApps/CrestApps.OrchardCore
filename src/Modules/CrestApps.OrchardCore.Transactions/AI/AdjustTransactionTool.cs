using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Lowers what is owed on a transaction without money being received, through the Adjust action of its page
/// (<see cref="ITransactionCommandService.AdjustAsync"/>). A write-off or a waiver gives up money owed and the adjustment
/// stays on the timeline for good, so the tool follows the owner's money rules (F23): the amount and the transaction's
/// currency are always given, taking off everything still owed needs <c>entireBalance</c>, the reason never holds a card
/// or bank account number, and every call carries the token <c>previewTransactionAction</c> issued for the same
/// adjustment and an idempotency key. The key is looked up first, so a retry of an adjustment that went through gets its
/// first answer back instead of a refusal about the balance it lowered; the checks and the token are taken inside the
/// operation, and a refusal keeps the key free.
/// </summary>
internal sealed class AdjustTransactionTool : TransactionMoneyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "adjustTransaction";

    private const string Refused = "Nothing was adjusted";

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
          "description": "How much to take off what is owed, more than zero and no more than what is still owed, in the transaction's currency."
        },
        "currency": {
          "type": "string",
          "description": "The transaction's currency, such as USD. Another currency is refused."
        },
        "kind": {
          "type": "string",
          "enum": ["discount", "waiver", "credit", "writeOff"],
          "description": "Why the amount is taken off."
        },
        "reason": {
          "type": "string",
          "description": "The reason, kept on the transaction's timeline and shown on the invoice, up to 500 characters. Never a card or bank account number."
        },
        "entireBalance": {
          "type": "boolean",
          "description": "Set to true to take off everything still owed; amount must then be exactly that. An amount equal to what is owed needs it."
        },
        "previewToken": {
          "type": "string",
          "description": "The previewToken previewTransactionAction returned for this transaction with the adjust action and the same amount, currency, kind and entireBalance. It works once and expires 10 minutes after the preview."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this adjustment, up to 128 characters. A second call with the same key returns the first answer, so a retry never adjusts twice."
        }
      },
      "required": ["transactionId", "amount", "currency", "kind", "previewToken", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lowers what is owed on a transaction without money being received, as a discount, waiver, credit or write-off, with a reason; everything still owed needs entireBalance. The adjustment stays on the timeline and only reverseTransactionEntry takes it back. Call previewTransactionAction with the adjust action first and show the result to the user: this tool needs the previewToken it returns and a new idempotencyKey.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadKey(arguments);

        if (key is null)
        {
            return $"{Refused}: give a new idempotencyKey of up to {MaxKeyLength} characters for this adjustment.";
        }

        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        var (reason, reasonError) = ReadText(arguments, "reason", required: false, Refused);

        if (reasonError is not null)
        {
            return reasonError;
        }

        var previewToken = ReadString(arguments, "previewToken");

        return await RunOnceAsync(services, caller, key, RequestFingerprint(AdjustAction, transaction, arguments), "adjusted", async token =>
        {
            var (request, refusal) = await ReadActionAsync(services, arguments, transaction, AdjustAction, "adjusted", token);

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
                result = await services.GetRequiredService<ITransactionCommandService>().AdjustAsync(transaction.ItemId, request.Amount, request.Kind.Value, reason, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new IdempotentOperationAnswer("The adjustment stopped with an error, and it may already be recorded. Do not retry with this or another key; read the transaction with getTransaction.", Keep: true);
            }

            if (!result.Succeeded)
            {
                return new IdempotentOperationAnswer($"{Refused}: {string.Join(" ", result.Errors)}", Keep: false);
            }

            var current = result.Transaction ?? transaction;
            var answer = await DescribeAsync(services, current);
            answer["adjustedBy"] = result.Event?.Amount;
            answer["kind"] = ToCamel(request.Kind.Value.ToString());
            answer["message"] = $"{Format(result.Event?.Amount ?? request.Amount, current.Currency)} was taken off what is owed.";

            return new IdempotentOperationAnswer(Json(answer), Keep: true);
        }, cancellationToken);
    }
}
