using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutReconciliationService"/>. It verifies every non-terminal durable
/// attempt against the owning provider's authoritative API, only settles an obligation when the provider
/// confirms it <em>and</em> the confirmed money matches what the attempt expected, and rebuilds the
/// session's payment metadata as a pure projection of the durable ledger. This is what guarantees the
/// checkout never shows an obligation as paid when the gateway actually failed, never accepts an
/// underpayment as settlement, and never loses a real charge because a cache entry expired.
/// </summary>
public sealed class CheckoutReconciliationService : ICheckoutReconciliationService
{
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly ILogger<CheckoutReconciliationService> _logger;

    public CheckoutReconciliationService(
        IPaymentAttemptStore attemptStore,
        ICheckoutPaymentProviderResolver providerResolver,
        ILogger<CheckoutReconciliationService> logger)
    {
        _attemptStore = attemptStore;
        _providerResolver = providerResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CheckoutReconciliationResult> ReconcileAsync(
        CheckoutSession session,
        IEnumerable<string> expectedObligationIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(expectedObligationIds);

        var result = new CheckoutReconciliationResult();

        var attempts = (await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken)).ToList();

        foreach (var attempt in attempts)
        {
            if (attempt.State is PaymentAttemptState.Succeeded
                or PaymentAttemptState.Failed
                or PaymentAttemptState.Canceled)
            {
                continue;
            }

            var provider = _providerResolver.GetProvider(attempt.ProviderKey);

            if (provider == null)
            {
                _logger.LogWarning("No checkout payment provider registered for key '{ProviderKey}'; attempt '{AttemptId}' cannot be verified and its obligation stays outstanding.", attempt.ProviderKey, attempt.ItemId);

                continue;
            }

            var verification = await provider.VerifyAsync(
                new VerifyPaymentContext
                {
                    Session = session,
                    Attempt = attempt,
                },
                cancellationToken);

            await ApplyVerificationAsync(attempt, verification, cancellationToken);
        }

        RebuildPaymentsMetadata(session, attempts);

        var settled = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);

        // An obligation is only settled by an attempt that is both succeeded and carries a provider
        // transaction id. This keeps obligation evaluation in lock-step with the payment-metadata
        // projection below, so an inconsistent "succeeded but no transaction id" record can never complete
        // a checkout while contributing no payment record.
        foreach (var attempt in attempts)
        {
            if (attempt.State == PaymentAttemptState.Succeeded && !string.IsNullOrEmpty(attempt.TransactionId))
            {
                settled.Add(attempt.ObligationId);
            }
            else if (attempt.State is PaymentAttemptState.Failed or PaymentAttemptState.Canceled)
            {
                failed.Add(attempt.ObligationId);
            }
        }

        // Evaluate the union of the caller's expected obligations and every obligation that actually has a
        // durable attempt, so a charge that exists but was omitted from the expected set can never be
        // silently dropped and reported as fully settled.
        var universe = new HashSet<string>(expectedObligationIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        universe.UnionWith(attempts.Select(a => a.ObligationId).Where(id => !string.IsNullOrEmpty(id)));

        foreach (var obligationId in universe)
        {
            if (settled.Contains(obligationId))
            {
                result.SettledObligationIds.Add(obligationId);
            }
            else if (failed.Contains(obligationId))
            {
                result.FailedObligationIds.Add(obligationId);
            }
            else
            {
                result.OutstandingObligationIds.Add(obligationId);
            }
        }

        result.IsFullySettled = result.OutstandingObligationIds.Count == 0 && result.FailedObligationIds.Count == 0;

        return result;
    }

    private async Task ApplyVerificationAsync(
        PaymentAttempt attempt,
        PaymentVerificationResult verification,
        CancellationToken cancellationToken)
    {
        switch (verification.Status)
        {
            case PaymentStatus.Succeeded:
                if (!TryValidateSettlement(attempt, verification, out var rejection))
                {
                    // The provider claims success but the money does not match what we expected. Never mark
                    // this obligation as paid on a discrepancy; keep the attempt non-terminal, record the
                    // reason for audit, and leave the obligation outstanding for manual reconciliation.
                    attempt.FailureReason = rejection;
                    await _attemptStore.UpdateAsync(attempt, cancellationToken);

                    _logger.LogError("Provider '{ProviderKey}' reported attempt '{AttemptId}' as succeeded but the confirmation was rejected: {Reason}. The obligation stays outstanding.", attempt.ProviderKey, attempt.ItemId, rejection);

                    break;
                }

                attempt.State = PaymentAttemptState.Succeeded;
                attempt.TransactionId = verification.TransactionId;
                attempt.GatewayMode = verification.GatewayMode;
                attempt.ConfirmedAmount = verification.ReportsAuthoritativeAmount ? verification.Amount : attempt.ExpectedAmount;
                attempt.ConfirmedTaxAmount = verification.ReportsAuthoritativeAmount ? verification.TaxAmount : attempt.ExpectedTaxAmount;
                attempt.TaxSnapshot = verification.TaxSnapshot ?? attempt.TaxSnapshot;
                attempt.FailureReason = null;
                await _attemptStore.UpdateAsync(attempt, cancellationToken);

                break;

            case PaymentStatus.Failed:
                attempt.State = PaymentAttemptState.Failed;
                await _attemptStore.UpdateAsync(attempt, cancellationToken);

                break;

            default:
                // The provider does not yet have an authoritative answer; leave the attempt pending so a
                // later reconciliation (or webhook) can settle it. The obligation stays outstanding.
                break;
        }
    }

    private static bool TryValidateSettlement(PaymentAttempt attempt, PaymentVerificationResult verification, out string rejection)
    {
        if (string.IsNullOrEmpty(verification.TransactionId))
        {
            rejection = "the provider returned no transaction id";

            return false;
        }

        if (!verification.ReportsAuthoritativeAmount)
        {
            // A deferred provider (for example Pay Later) never moves money at a processor, so there is no
            // authoritative charged amount to cross-check. The transaction id is enough to record it.
            rejection = null;

            return true;
        }

        if (!string.Equals(verification.Currency, attempt.Currency, StringComparison.OrdinalIgnoreCase))
        {
            rejection = $"the provider charged in '{verification.Currency}' but the attempt expected '{attempt.Currency}'";

            return false;
        }

        var chargedBase = Money.ToMinorUnits(verification.Amount, attempt.Currency);
        var expectedBase = Money.ToMinorUnits(attempt.ExpectedAmount, attempt.Currency);

        if (chargedBase < expectedBase)
        {
            rejection = $"the provider charged {verification.Amount} but the attempt expected at least {attempt.ExpectedAmount}";

            return false;
        }

        rejection = null;

        return true;
    }

    private static void RebuildPaymentsMetadata(CheckoutSession session, IEnumerable<PaymentAttempt> attempts)
    {
        // The session's payment metadata is a pure projection of the durable ledger, rebuilt on every
        // reconciliation. Because the ledger is the source of truth, this is idempotent and can never
        // strand or double-count a confirmed charge, even if a previous reconciliation crashed midway.
        var metadata = new PaymentsMetadata();

        foreach (var attempt in attempts)
        {
            if (attempt.State != PaymentAttemptState.Succeeded || string.IsNullOrEmpty(attempt.TransactionId))
            {
                continue;
            }

            metadata.Payments[attempt.TransactionId] = new PaymentRecord
            {
                Status = PaymentStatus.Succeeded,
                Amount = attempt.ConfirmedAmount,
                TaxAmount = attempt.ConfirmedTaxAmount,
                TaxSnapshot = attempt.TaxSnapshot,
                Currency = attempt.Currency,
                ObligationId = attempt.ObligationId,
                GatewayId = attempt.ProviderKey,
                GatewayMode = attempt.GatewayMode,
                TransactionId = attempt.TransactionId,
            };
        }

        session.Put(metadata);
    }
}
