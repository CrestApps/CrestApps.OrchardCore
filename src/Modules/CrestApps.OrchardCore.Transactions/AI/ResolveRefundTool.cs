using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Marks a refund that waits for manual review as settled, with the reference of the money returned outside the site,
/// through the Resolve action of Commerce › Refunds (<see cref="TransactionAdminActions.SettleRefundByHandAsync"/>),
/// which completes the refund on its order or transaction. It moves no money, only records that it was moved, so a
/// reference is required (stricter than the page, which takes an empty one) and it asks for approval. Committed at once.
/// </summary>
internal sealed class ResolveRefundTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resolveRefund";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "refundId": {
          "type": "string",
          "description": "The refund's id, from listRefunds with status pendingManualReview."
        },
        "reference": {
          "type": "string",
          "description": "The reference of the money returned by hand, such as a bank transfer number. Required."
        }
      },
      "required": ["refundId", "reference"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a refund that is waiting for manual review as settled, with the reference of the money returned outside the site. Use it only after the customer was actually paid back; it cannot be undone and it completes the refund on the order or transaction.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var reference = ReadString(arguments, "reference");

        if (reference is null)
        {
            return "Nothing was settled: give the reference of the money returned by hand, such as the bank transfer number.";
        }

        var refundId = ReadString(arguments, "refundId");
        var refundStore = services.GetRequiredService<IPaymentRefundStore>();
        var refund = refundId is null ? null : await refundStore.FindByIdAsync(refundId, cancellationToken);

        if (refund is null)
        {
            return $"Nothing was settled: no refund has the id '{refundId}'. Use listRefunds to find it.";
        }

        var logger = (ILogger)services.GetService<ILogger<ResolveRefundTool>>() ?? NullLogger.Instance;
        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        if (!await TransactionAdminActions.SettleRefundByHandAsync(services, refundStore, refund, reference, utcNow, logger, cancellationToken))
        {
            return $"Nothing was settled: the refund is {ToCamel(refund.Status.ToString())}; only a refund waiting for manual review can be settled by hand.";
        }

        var session = services.GetRequiredService<ISession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was settled: the refund or its order changed at the same moment. Read it again with listRefunds and retry in your next message.";
        }

        return Json(new
        {
            id = refund.ItemId,
            status = ToCamel(refund.Status.ToString()),
            amount = refund.RefundGrossAmount,
            currency = refund.Currency,
            reference = refund.ProviderRefundReference,
            settledAt = await LocalTimeAsync(services, refund.CompletedUtc),
            message = "The refund was marked as settled. No money moved through the site.",
        });
    }
}
