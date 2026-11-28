using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Lists refunds as Commerce › Refunds does (<see cref="IPaymentRefundStore.PageAsync"/>), including those waiting to
/// be settled by hand.
/// </summary>
internal sealed class ListRefundsTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listRefunds";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["any", "requested", "pending", "succeeded", "failed", "canceled", "pendingManualReview"],
          "description": "Which refunds to list; any by default. pendingManualReview lists those waiting to be settled by hand."
        },
        "paymentMethod": {
          "type": "string",
          "description": "The payment method's key, such as stripe."
        },
        "checkoutSessionId": {
          "type": "string",
          "description": "Only the refunds of one checkout."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Refunds per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists refunds with date, payment method, status (including those waiting to be settled by hand), amount, reason and provider reference. Filters by status, payment method and checkout, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status") ?? "any";
        RefundStatus? filter = null;

        if (!string.Equals(status, "any", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<RefundStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return "Nothing was listed: status must be any, requested, pending, succeeded, failed, canceled or pendingManualReview.";
            }

            filter = parsed;
        }

        var (page, pageSize) = ReadPaging(arguments);
        var result = await services.GetRequiredService<IPaymentRefundStore>().PageAsync(page, pageSize, new PaymentRefundQuery
        {
            ProviderKey = ReadString(arguments, "paymentMethod"),
            Status = filter,
            SessionId = ReadString(arguments, "checkoutSessionId"),
        }, cancellationToken);

        var items = new List<object>();

        foreach (var refund in result.Entries)
        {
            items.Add(new
            {
                id = refund.ItemId,
                at = await LocalTimeAsync(services, refund.CreatedUtc),
                completedAt = await LocalTimeAsync(services, refund.CompletedUtc),
                paymentMethod = refund.ProviderKey,
                status = ToCamel(refund.Status.ToString()),
                amount = refund.RefundGrossAmount,
                currency = refund.Currency,
                reason = refund.Reason,
                providerReference = refund.ProviderRefundReference,
                failureReason = refund.FailureReason,
                checkoutSessionId = refund.SessionId,
                referenceType = refund.ReferenceType,
                referenceId = refund.ReferenceId,
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
