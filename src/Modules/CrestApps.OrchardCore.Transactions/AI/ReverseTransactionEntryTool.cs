using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Takes back a payment recorded by hand, or an adjustment, through the Take back action of the transaction's timeline
/// (<see cref="ITransactionCommandService.ReverseAsync"/>, which locks, checks and commits, then moves the customer credit
/// the entry moved). What the entry paid or took off is owed again, both entries stay on the timeline, and the reversal
/// cannot itself be taken back, so the tool follows the owner's money rules (F23): the entry's amount and the
/// transaction's currency are given, the reason never holds a card or bank account number, and the call carries the token
/// <c>previewTransactionAction</c> issued for the same entry. The command refuses a second reversal of an entry and the
/// token works once, so no idempotency key is needed.
/// </summary>
internal sealed class ReverseTransactionEntryTool : TransactionMoneyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "reverseTransactionEntry";

    private const string Refused = "Nothing was taken back";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions."
        },
        "eventId": {
          "type": "string",
          "description": "The id of the timeline entry to take back (a payment recorded by hand or an adjustment), from getTransaction's timeline."
        },
        "amount": {
          "type": "number",
          "description": "The entry's amount: an entry is taken back whole."
        },
        "currency": {
          "type": "string",
          "description": "The transaction's currency, such as USD."
        },
        "reason": {
          "type": "string",
          "description": "Why the entry is taken back, such as 'Check returned unpaid', up to 500 characters. It is kept on the timeline. Never a card or bank account number."
        },
        "previewToken": {
          "type": "string",
          "description": "The previewToken previewTransactionAction returned for this transaction with the reverse action, the same eventId, amount and currency. It works once and expires 10 minutes after the preview."
        }
      },
      "required": ["transactionId", "eventId", "amount", "currency", "reason", "previewToken"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Takes back a payment recorded by hand, or an adjustment, that should not have been recorded (a returned check, a payment on the wrong transaction): what it paid or took off is owed again, and customer credit the payment moved moves back. Both entries stay on the timeline and the reversal can't be undone. A payment taken online is refunded instead. Call previewTransactionAction with the reverse action first and show the result to the user: this tool needs the previewToken it returns.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        var (request, refusal) = await ReadActionAsync(services, arguments, transaction, ReverseAction, "taken back", cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        var (reason, reasonError) = ReadText(arguments, "reason", required: true, Refused, "say why the entry is taken back.");

        if (reasonError is not null)
        {
            return reasonError;
        }

        if (await TakePreviewTokenAsync(services, caller, ReadString(arguments, "previewToken"), request.Fingerprint, Refused, cancellationToken) is { } tokenError)
        {
            return tokenError;
        }

        TransactionCommandResult result;

        try
        {
            result = await services.GetRequiredService<ITransactionCommandService>().ReverseAsync(transaction.ItemId, request.Entry.Id, reason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return "Taking the entry back stopped with an error, and it may already be taken back. Do not retry; read the transaction with getTransaction.";
        }

        if (!result.Succeeded)
        {
            return $"{Refused}: {string.Join(" ", result.Errors)}";
        }

        var current = result.Transaction ?? transaction;
        var answer = await DescribeAsync(services, current);
        answer["eventId"] = request.Entry.Id;
        answer["reversalId"] = result.Event?.Id;
        answer["takenBack"] = request.Amount;
        answer["creditChange"] = request.CreditChange == 0m ? null : request.CreditChange;
        answer["warnings"] = result.Warnings.Count > 0 ? result.Warnings.ToList() : null;
        answer["adminPath"] = GetAdminPath(services, "TransactionsDetail", new { action = "Detail", itemId = current.ItemId });
        answer["message"] = $"The entry of {Format(request.Amount, current.Currency)} was taken back, so it is owed again{CreditSentence(request.CreditChange, current.Currency)}.";

        return Json(answer);
    }

    private static string CreditSentence(decimal creditChange, string currency)
        => creditChange switch
        {
            > 0m => $"; {Format(creditChange, currency)} was given back to the customer's credit",
            < 0m => $"; the {Format(-creditChange, currency)} it had added to the customer's credit was taken back",
            _ => string.Empty,
        };
}
