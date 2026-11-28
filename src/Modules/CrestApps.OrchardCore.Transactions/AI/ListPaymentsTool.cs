using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Lists payment attempts as Commerce › Payments does (<see cref="IPaymentAttemptStore.PageAsync"/>), with, for each
/// payment the refund form would open, what was already refunded and what can still be refunded, and whether it belongs
/// to an order (refunded with <c>refundOrder</c>, not <c>refundPayment</c>).
/// </summary>
internal sealed class ListPaymentsTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPayments";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "state": {
          "type": "string",
          "enum": ["any", "created", "pending", "succeeded", "failed", "canceled"],
          "description": "Which payments to list; any by default."
        },
        "paymentMethod": {
          "type": "string",
          "description": "The payment method's key, such as stripe."
        },
        "checkoutSessionId": {
          "type": "string",
          "description": "Only the payments of one checkout."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Payments per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists payment attempts with date, payment method, state, amount, the checkout and what they paid for, and for settled ones what was already refunded and what can still be refunded. Filters by state, payment method and checkout, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var state = ReadString(arguments, "state") ?? "any";
        PaymentAttemptState? filter = null;

        if (!string.Equals(state, "any", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<PaymentAttemptState>(state, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return "Nothing was listed: state must be any, created, pending, succeeded, failed or canceled.";
            }

            filter = parsed;
        }

        var (page, pageSize) = ReadPaging(arguments);
        var result = await services.GetRequiredService<IPaymentAttemptStore>().PageAsync(page, pageSize, new PaymentAttemptQuery
        {
            ProviderKey = ReadString(arguments, "paymentMethod"),
            State = filter,
            SessionId = ReadString(arguments, "checkoutSessionId"),
        }, cancellationToken);

        var refundStore = services.GetRequiredService<IPaymentRefundStore>();
        var displayText = services.GetRequiredService<TransactionDisplayText>();
        var items = new List<object>();

        foreach (var attempt in result.Entries)
        {
            var canRefund = PaymentAttemptRefundPolicy.CanRefund(attempt, displayText);
            decimal? refunded = null;
            decimal? refundable = null;

            if (canRefund)
            {
                (var total, _) = await GetRefundedAsync(refundStore, attempt, cancellationToken);
                refunded = total;
                refundable = CurrencyScale.Round(Math.Max(0m, Settled(attempt) - total), attempt.Currency);
            }

            items.Add(new
            {
                id = attempt.ItemId,
                at = await LocalTimeAsync(services, attempt.CreatedUtc),
                paymentMethod = attempt.ProviderKey,
                paymentMethodName = displayText.PaymentMethodName(attempt.ProviderKey),
                state = ToCamel(attempt.State.ToString()),
                amount = attempt.State == PaymentAttemptState.Succeeded ? Settled(attempt) : attempt.ExpectedAmount + attempt.ExpectedTaxAmount,
                currency = attempt.Currency,
                checkoutSessionId = attempt.SessionId,
                referenceType = attempt.ReferenceType,
                referenceId = attempt.ReferenceId,
                belongsToOrder = string.Equals(attempt.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal),
                failureReason = attempt.FailureReason,
                refunded,
                refundable,
            });
        }

        return Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = page * pageSize < result.Count,
        });
    }
}
