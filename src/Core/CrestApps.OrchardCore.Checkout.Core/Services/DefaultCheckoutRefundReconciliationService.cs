using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutRefundReconciliationService"/>. It applies a refund observed at a
/// gateway to the durable refund ledger under the same distributed lock the refund service uses, so a
/// remote notification can never race a locally initiated refund of the same payment. It correlates the
/// remote refund to a local record by provider reference, idempotency key, or a still-open local request
/// for the same transaction, and quarantines an unmatched remote refund as <see cref="RefundStatus.PendingManualReview"/>
/// instead of losing it or fabricating a refund the application never requested.
/// </summary>
public sealed class DefaultCheckoutRefundReconciliationService : ICheckoutRefundReconciliationService
{
    private const string RefundLockPrefix = "CHECKOUT_REFUND_";

    private readonly IPaymentRefundStore _refundStore;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutRefundReconciliationService"/> class.
    /// </summary>
    /// <param name="refundStore">The durable refund ledger.</param>
    /// <param name="distributedLock">The distributed lock used to serialize refunds of a payment.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCheckoutRefundReconciliationService(
        IPaymentRefundStore refundStore,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DefaultCheckoutRefundReconciliationService> logger)
    {
        _refundStore = refundStore;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PaymentRefund> ReconcileRemoteRefundAsync(ReconcileRemoteRefundContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.OriginalTransactionId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            RefundLockPrefix + context.OriginalTransactionId,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(2));

        if (!locked)
        {
            throw new InvalidOperationException($"Could not acquire a refund lock for transaction '{context.OriginalTransactionId}'.");
        }

        await using (locker)
        {
            var local = await CorrelateAsync(context, cancellationToken);

            if (local is not null)
            {
                return await UpdateLocalRefundAsync(local, context, cancellationToken);
            }

            return await QuarantineRemoteRefundAsync(context, cancellationToken);
        }
    }

    private async Task<PaymentRefund> CorrelateAsync(ReconcileRemoteRefundContext context, CancellationToken cancellationToken)
    {
        // The gateway's own refund reference is the strongest correlation: it is unique and is persisted on
        // the local record as soon as a locally initiated refund is accepted by the provider.
        if (!string.IsNullOrEmpty(context.ProviderRefundReference))
        {
            var byReference = await _refundStore.GetByProviderRefundReferenceAsync(context.ProviderRefundReference, cancellationToken);

            if (byReference is not null && IsSameScope(byReference, context))
            {
                return byReference;
            }
        }

        // A locally initiated refund seeds a deterministic idempotency key before the provider is called, so
        // the gateway can echo it back even before the provider reference was persisted locally.
        if (!string.IsNullOrEmpty(context.IdempotencyKey))
        {
            var byIdempotency = await _refundStore.GetByIdempotencyKeyAsync(context.IdempotencyKey, cancellationToken);

            if (byIdempotency is not null && IsSameScope(byIdempotency, context))
            {
                return byIdempotency;
            }
        }

        // A gateway that does not echo the idempotency key on a dedicated field may still echo it in the
        // refund metadata, so correlate on the well-known metadata key when it is present.
        if (context.Metadata is not null &&
            context.Metadata.TryGetValue(CheckoutRefundMetadataKeys.IdempotencyKey, out var metadataIdempotencyKey) &&
            !string.IsNullOrEmpty(metadataIdempotencyKey))
        {
            var byMetadata = await _refundStore.GetByIdempotencyKeyAsync(metadataIdempotencyKey, cancellationToken);

            if (byMetadata is not null && IsSameScope(byMetadata, context))
            {
                return byMetadata;
            }
        }

        // An aggregate notification (a charge-level refund total with no per-refund identity) carries neither
        // a provider reference nor an echoed idempotency key. Correlate repeated deliveries of the same
        // aggregate to the one record they already produced, so an out-of-band refund is quarantined once
        // instead of duplicated on every webhook retry.
        if (string.IsNullOrEmpty(context.ProviderRefundReference) && string.IsNullOrEmpty(context.IdempotencyKey))
        {
            var byAggregateKey = await _refundStore.GetByIdempotencyKeyAsync(BuildAggregateCorrelationKey(context), cancellationToken);

            if (byAggregateKey is not null && IsSameScope(byAggregateKey, context))
            {
                return byAggregateKey;
            }
        }

        // Fall back to a still-open local request for the same payment that has not yet captured a provider
        // reference (for example the provider call returned pending). Match the refunded amount at currency
        // minor-unit precision, and require the same currency, so a partial refund is not attached to the
        // wrong request.
        var priorRefunds = await _refundStore.GetByOriginalTransactionAsync(context.OriginalTransactionId, cancellationToken);

        var remoteMinorUnits = CurrencyScale.ToMinorUnits(context.RefundedAmount, context.Currency);

        return priorRefunds.FirstOrDefault(r =>
            string.IsNullOrEmpty(r.ProviderRefundReference) &&
            (r.Status is RefundStatus.Requested or RefundStatus.Pending) &&
            string.Equals(r.Currency, context.Currency, StringComparison.OrdinalIgnoreCase) &&
            CurrencyScale.ToMinorUnits(r.RefundGrossAmount, r.Currency) == remoteMinorUnits);
    }

    private async Task<PaymentRefund> UpdateLocalRefundAsync(PaymentRefund local, ReconcileRemoteRefundContext context, CancellationToken cancellationToken)
    {
        // Adopt the gateway's authoritative reference and status. The gateway is the source of truth, so a
        // pending local record advances to the confirmed terminal state the gateway reports.
        if (string.IsNullOrEmpty(local.ProviderRefundReference) && !string.IsNullOrEmpty(context.ProviderRefundReference))
        {
            local.ProviderRefundReference = context.ProviderRefundReference;
        }

        // Never regress a refund that is already flagged for manual review from a webhook: an operator must
        // clear that state after allocating tax. Never let a webhook mutate a refund that already reached a
        // terminal state: the terminal result was confirmed by the gateway and a stale or out-of-order event
        // must not flip it (for example Succeeded to Failed). Only a non-terminal, non-quarantined local
        // record is advanced to the gateway's reported status.
        if (local.Status != RefundStatus.PendingManualReview && !IsTerminal(local.Status))
        {
            local.Status = context.Status;

            if (context.Status is RefundStatus.Failed)
            {
                local.FailureReason ??= context.Reason;
            }

            if (context.Status is RefundStatus.Succeeded or RefundStatus.Failed or RefundStatus.Canceled)
            {
                local.CompletedUtc ??= _clock.UtcNow;
            }
        }

        // Keep a quarantined aggregate (a charge-level refund total with no per-refund identity) in step
        // with the gateway's latest cumulative refunded total, so a redelivered aggregate updates the one
        // manual-review record instead of stranding a stale amount. A single stable record is used rather
        // than one amount-keyed record per delivery, so the cumulative total is never double-counted. The
        // cumulative refunded total only ever grows at the gateway, so only a greater total is adopted; an
        // out-of-order redelivery carrying an older, smaller total is ignored instead of regressing the
        // recorded amount.
        if (string.IsNullOrEmpty(context.ProviderRefundReference) &&
            string.IsNullOrEmpty(context.IdempotencyKey) &&
            local.Status == RefundStatus.PendingManualReview &&
            string.Equals(local.FailureCode, CheckoutRefundFailureCodes.RemoteRefundWithoutLocalRequest, StringComparison.Ordinal) &&
            CurrencyScale.ToMinorUnits(context.RefundedAmount, context.Currency) > CurrencyScale.ToMinorUnits(local.RefundGrossAmount, local.Currency))
        {
            local.RefundGrossAmount = context.RefundedAmount;
            local.RefundTaxableAmount = context.RefundedAmount;
        }

        await _refundStore.UpdateAsync(local, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Reconciled remote refund '{ProviderRefundReference}' for transaction '{TransactionId}' onto local refund '{RefundId}' with status '{Status}'.",
                context.ProviderRefundReference,
                context.OriginalTransactionId,
                local.ItemId,
                local.Status);
        }

        return local;
    }

    private async Task<PaymentRefund> QuarantineRemoteRefundAsync(ReconcileRemoteRefundContext context, CancellationToken cancellationToken)
    {
        // The gateway refunded a payment this application never requested (for example a refund issued from
        // the provider dashboard). Persist it so it is never lost, but leave it in manual review: its tax is
        // not allocated and it is not attached to a session or attempt, which an operator must resolve.
        var refund = new PaymentRefund
        {
            ItemId = IdGenerator.GenerateId(),
            ProviderKey = context.ProviderKey,
            OriginalTransactionId = context.OriginalTransactionId,
            ProviderRefundReference = context.ProviderRefundReference,
            Currency = context.Currency,
            RefundGrossAmount = context.RefundedAmount,
            RefundTaxableAmount = context.RefundedAmount,
            RefundTaxAmount = 0m,
            Status = RefundStatus.PendingManualReview,
            Reason = context.Reason,
            GatewayMode = context.GatewayMode,
            IdempotencyKey = ResolveQuarantineIdempotencyKey(context),
            FailureCode = CheckoutRefundFailureCodes.RemoteRefundWithoutLocalRequest,
            FailureReason = "A refund exists at the gateway with no matching local refund request and must be reviewed by an operator.",
        };

        await _refundStore.CreateAsync(refund, cancellationToken);

        _logger.LogWarning(
            "Remote refund '{ProviderRefundReference}' for transaction '{TransactionId}' had no local request and was recorded for manual review as refund '{RefundId}'.",
            context.ProviderRefundReference,
            context.OriginalTransactionId,
            refund.ItemId);

        return refund;
    }

    // Stamps a quarantined remote refund with a stable idempotency key so a redelivered webhook correlates
    // to the existing manual-review record instead of creating a duplicate. A provider reference or an
    // echoed idempotency key is preferred; an identity-less aggregate falls back to a deterministic key
    // derived from the transaction and refunded amount.
    private static string ResolveQuarantineIdempotencyKey(ReconcileRemoteRefundContext context)
    {
        if (!string.IsNullOrEmpty(context.IdempotencyKey))
        {
            return context.IdempotencyKey;
        }

        if (!string.IsNullOrEmpty(context.ProviderRefundReference))
        {
            return "remote_refund_" + context.ProviderRefundReference;
        }

        return BuildAggregateCorrelationKey(context);
    }

    // Builds the deterministic correlation key for an identity-less aggregate refund notification so
    // repeated deliveries of the same charge-level refund total resolve to a single record. The key is
    // scoped to the provider, gateway mode, transaction, and currency (never the amount), so a later
    // cumulative total updates the same record instead of creating a second amount-keyed record that would
    // overstate the refund, while a colliding transaction identifier from a different provider or from the
    // opposite (test or live) mode resolves to a distinct record.
    private static string BuildAggregateCorrelationKey(ReconcileRemoteRefundContext context)
        => string.Concat(
            "remote_refund_aggregate_",
            context.ProviderKey?.ToUpperInvariant(),
            "_",
            context.GatewayMode,
            "_",
            context.OriginalTransactionId,
            "_",
            context.Currency?.ToUpperInvariant());

    private static bool IsTerminal(RefundStatus status)
        => status is RefundStatus.Succeeded or RefundStatus.Failed or RefundStatus.Canceled;

    // Guards a correlation found by a globally unique key (a provider reference or an idempotency key) so it
    // is only accepted when it belongs to the same original transaction, the same provider, and the same
    // gateway mode. A reference or key collision across providers, or across test and live modes, is thus
    // never allowed to mutate an unrelated local refund; an ambiguous match is left to fall through and be
    // quarantined for manual review instead. Each field is compared only when both sides carry it, so an
    // as-yet-unpopulated local field never blocks a legitimate correlation.
    private static bool IsSameScope(PaymentRefund local, ReconcileRemoteRefundContext context)
    {
        if (!string.IsNullOrEmpty(local.OriginalTransactionId) &&
            !string.IsNullOrEmpty(context.OriginalTransactionId) &&
            !string.Equals(local.OriginalTransactionId, context.OriginalTransactionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(local.ProviderKey) &&
            !string.IsNullOrEmpty(context.ProviderKey) &&
            !string.Equals(local.ProviderKey, context.ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return local.GatewayMode == context.GatewayMode;
    }
}
