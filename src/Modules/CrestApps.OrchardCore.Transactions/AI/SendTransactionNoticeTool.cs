using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Sends a transaction's owner its invoice with the link that pays it (<see cref="ITransactionCommandService.SendInvoiceAsync"/>)
/// or a payment reminder (<see cref="ITransactionReminderService.SendReminderAsync"/>, only with the Transaction
/// Reminders feature and an outstanding balance, then saved as the page does), through the Send invoice and Send
/// reminder actions of the transaction's page.
/// </summary>
internal sealed class SendTransactionNoticeTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendTransactionNotice";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions."
        },
        "notice": {
          "type": "string",
          "enum": ["invoice", "reminder"],
          "description": "invoice sends the invoice with its pay link; reminder sends a payment reminder (needs the Transaction Reminders feature and an outstanding balance)."
        }
      },
      "required": ["transactionId", "notice"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends the transaction's owner either its invoice with the link that pays it, or a payment reminder. A reminder needs the Transaction Reminders feature and an outstanding balance.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var notice = ReadString(arguments, "notice")?.ToLowerInvariant();

        if (notice is not ("invoice" or "reminder"))
        {
            return "Nothing was sent: notice must be invoice or reminder.";
        }

        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        if (notice == "invoice")
        {
            var result = await services.GetRequiredService<ITransactionCommandService>().SendInvoiceAsync(transaction.ItemId, cancellationToken);

            return result.Succeeded
                ? Json(new { id = transaction.ItemId, notice, sent = true, message = "The invoice was sent to the transaction's owner." })
                : $"Nothing was sent: {string.Join(" ", result.Errors)}";
        }

        if (services.GetService<ITransactionReminderService>() is not { } reminders)
        {
            return "Nothing was sent: reminders are unavailable. Turn on the Transaction Reminders feature to send reminders.";
        }

        if (!TransactionStateMachine.CanSendReminder(transaction))
        {
            return "Nothing was sent: this transaction has no outstanding balance to chase.";
        }

        if (!await reminders.SendReminderAsync(transaction, cancellationToken))
        {
            return "Nothing was sent: the reminder could not be sent. The owner may not have a notification channel set up.";
        }

        if (await SaveAsync(services, transaction, cancellationToken) is { } saveError)
        {
            return $"The reminder was sent, but the transaction's record of it was not saved. {saveError}";
        }

        return Json(new
        {
            id = transaction.ItemId,
            notice,
            sent = true,
            remindersSent = transaction.ReminderCount,
            message = "A payment reminder was sent to the transaction's owner.",
        });
    }
}
