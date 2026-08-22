using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// A provider-neutral payment event handler that routes every refund observed at a gateway into the
/// durable refund ledger through <see cref="ICheckoutRefundReconciliationService"/>. This is how a remote
/// refund (including one issued out-of-band from a provider dashboard) is correlated to, or quarantined
/// against, the checkout's own refund records without the checkout depending on any specific provider.
/// </summary>
public sealed class RefundReconciliationPaymentEventHandler : PaymentEventBase
{
    private readonly ICheckoutRefundReconciliationService _reconciliationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefundReconciliationPaymentEventHandler"/> class.
    /// </summary>
    /// <param name="reconciliationService">The refund reconciliation service.</param>
    public RefundReconciliationPaymentEventHandler(ICheckoutRefundReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    /// <inheritdoc/>
    public override Task PaymentRefundedAsync(PaymentRefundedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.OriginalTransactionId))
        {
            return Task.CompletedTask;
        }

        return _reconciliationService.ReconcileRemoteRefundAsync(new ReconcileRemoteRefundContext
        {
            OriginalTransactionId = context.OriginalTransactionId,
            ProviderRefundReference = context.ProviderRefundReference,
            ProviderKey = context.GatewayId,
            RefundedAmount = context.RefundedAmount,
            Currency = context.Currency,
            Status = MapStatus(context.RefundStatus),
            Reason = context.Reason,
            IdempotencyKey = context.IdempotencyKey,
            Metadata = BuildMetadata(context.Data),
            GatewayMode = context.GatewayMode,
        });
    }

    private static Dictionary<string, string> BuildMetadata(Dictionary<string, object> data)
    {
        if (data is null || data.Count == 0)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in data)
        {
            if (pair.Value is not null)
            {
                metadata[pair.Key] = pair.Value.ToString();
            }
        }

        return metadata;
    }

    private static RefundStatus MapStatus(string gatewayStatus)
    {
        // A gateway that delivered a refund notification without a granular status has not confirmed a
        // terminal result, so the refund stays pending rather than fabricating a success the gateway never
        // reported. A later per-refund event (refund.updated/refund.failed) advances it to the real state.
        if (string.IsNullOrEmpty(gatewayStatus))
        {
            return RefundStatus.Pending;
        }

        return gatewayStatus.Trim().ToLowerInvariant() switch
        {
            "succeeded" or "success" or "complete" or "completed" => RefundStatus.Succeeded,
            "failed" or "failure" => RefundStatus.Failed,
            "canceled" or "cancelled" => RefundStatus.Canceled,
            "pending" or "processing" or "in_progress" => RefundStatus.Pending,
            _ => RefundStatus.Pending,
        };
    }
}
