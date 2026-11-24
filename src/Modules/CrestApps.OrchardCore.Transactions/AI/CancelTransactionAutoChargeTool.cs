using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Cancels the charge of a saved card scheduled on a transaction (a subscription renewal, an installment, a booking's
/// balance) through the Cancel the charge button of its page (<see cref="ISavedPaymentMethodChargeService.CancelAsync"/>,
/// which takes the charge's lock and commits), so the card is not charged and the owner pays the balance themselves. Only
/// server code schedules a charge, so no admin page can put it back, and the tool asks for approval.
/// </summary>
internal sealed class CancelTransactionAutoChargeTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelTransactionAutoCharge";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions; getTransaction's scheduledCharge shows its card charge."
        }
      },
      "required": ["transactionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels the charge of a saved card scheduled on a transaction (a subscription renewal, an installment, a booking balance), so the card is not charged and the owner pays the balance themselves, for example when the customer asked to pay another way. It can't be scheduled again from the admin.";

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

        if (transaction.AutoCharge is not { } charge)
        {
            return "Nothing was canceled: no card charge is scheduled on this transaction.";
        }

        var paymentMethod = transaction.AutoCollection?.PaymentMethodDescription;
        var nextAttemptAt = await LocalTimeAsync(services, charge.NextAttemptUtc);
        var declined = charge.Attempts;

        var result = await services.GetRequiredService<ISavedPaymentMethodChargeService>().CancelAsync(transaction.ItemId, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was canceled: {string.Join(" ", result.Errors)}";
        }

        var current = result.Transaction ?? transaction;
        var answer = await DescribeAsync(services, current);
        answer["canceledCharge"] = new
        {
            paymentMethod,
            nextAttemptAt,
            declinedSoFar = declined,
        };
        answer["canSendInvoice"] = TransactionStateMachine.CanSendInvoice(current);
        answer["adminPath"] = GetAdminPath(services, "TransactionsDetail", new { action = "Detail", itemId = current.ItemId });
        answer["message"] = "The scheduled card charge was canceled: the card is not charged, and the owner pays the balance themselves. Send them the invoice with its pay link with sendTransactionNotice if they need it.";

        return Json(answer);
    }
}
