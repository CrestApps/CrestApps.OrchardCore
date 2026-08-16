using System.Linq;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutRefundService"/>. It is the single authoritative entry point for
/// issuing a refund: it resolves the settled payment from the durable attempt ledger, derives the
/// refunded tax from the original payment's immutable tax snapshot (never from current rules), enforces
/// the remaining refundable amount, records the refund before calling the provider, and serializes
/// concurrent refunds of the same payment with a distributed lock so two nodes can never over-refund.
/// </summary>
public sealed class DefaultCheckoutRefundService : ICheckoutRefundService
{
    private const string RefundLockPrefix = "CHECKOUT_REFUND_";

    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly IPaymentRefundStore _refundStore;
    private readonly ICheckoutPaymentRefundProviderResolver _refundProviderResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IEnumerable<ITaxRefundCalculator> _taxRefundCalculators;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutRefundService"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="attemptStore">The durable payment attempt ledger.</param>
    /// <param name="refundStore">The durable refund ledger.</param>
    /// <param name="refundProviderResolver">The refund provider resolver.</param>
    /// <param name="distributedLock">The distributed lock used to serialize refunds of a payment.</param>
    /// <param name="taxRefundCalculators">The optional tax refund calculators contributed by the Taxation feature.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCheckoutRefundService(
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptStore attemptStore,
        IPaymentRefundStore refundStore,
        ICheckoutPaymentRefundProviderResolver refundProviderResolver,
        IDistributedLock distributedLock,
        IEnumerable<ITaxRefundCalculator> taxRefundCalculators,
        IClock clock,
        ILogger<DefaultCheckoutRefundService> logger)
    {
        _sessionStore = sessionStore;
        _attemptStore = attemptStore;
        _refundStore = refundStore;
        _refundProviderResolver = refundProviderResolver;
        _distributedLock = distributedLock;
        _taxRefundCalculators = taxRefundCalculators;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PaymentRefund> RequestRefundAsync(RequestPaymentRefundContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.SessionId);
        ArgumentException.ThrowIfNullOrEmpty(context.OriginalTransactionId);

        var session = await _sessionStore.GetAsync(context.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Checkout session '{context.SessionId}' was not found.");

        var attempts = await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken);

        var attempt = attempts.FirstOrDefault(a =>
            a.State == PaymentAttemptState.Succeeded &&
            string.Equals(a.TransactionId, context.OriginalTransactionId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No settled payment with transaction '{context.OriginalTransactionId}' was found on session '{context.SessionId}'.");

        var maxRefundableGross = (decimal)attempt.ConfirmedAmount + (decimal)attempt.ConfirmedTaxAmount;

        // Only one refund of the same payment may run at a time across all instances so two nodes can
        // never read each other's partial state and over-refund the charge.
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
            var priorRefunds = await _refundStore.GetByOriginalTransactionAsync(context.OriginalTransactionId, cancellationToken);

            var alreadyRefunded = priorRefunds
                .Where(r => r.Status is RefundStatus.Requested or RefundStatus.Pending or RefundStatus.Succeeded or RefundStatus.PendingManualReview)
                .Sum(r => r.RefundGrossAmount);

            var remaining = maxRefundableGross - alreadyRefunded;

            if (remaining <= 0)
            {
                throw new InvalidOperationException($"Payment '{context.OriginalTransactionId}' is already fully refunded.");
            }

            var requestedGross = CurrencyScale.Round(context.Amount ?? remaining, attempt.Currency);

            if (requestedGross <= 0)
            {
                throw new InvalidOperationException("The refund amount must be greater than zero.");
            }

            if (CurrencyScale.ToMinorUnits(requestedGross, attempt.Currency) > CurrencyScale.ToMinorUnits(remaining, attempt.Currency))
            {
                throw new InvalidOperationException($"The refund amount {requestedGross} exceeds the remaining refundable amount {remaining} for transaction '{context.OriginalTransactionId}'.");
            }

            var refund = BuildRefund(context, attempt, maxRefundableGross, alreadyRefunded, requestedGross);

            // Persist the refund BEFORE calling the provider so a crash can never strand a real refund.
            await _refundStore.CreateAsync(refund, cancellationToken);

            var provider = _refundProviderResolver.GetProvider(attempt.ProviderKey);

            if (provider is null)
            {
                // The owning provider has no executable refund operation (for example an offline
                // commitment). Record the refund for an operator to settle manually rather than
                // pretending it was processed.
                refund.Status = RefundStatus.PendingManualReview;

                await _refundStore.UpdateAsync(refund, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Refund '{RefundId}' for transaction '{TransactionId}' has no executable provider '{ProviderKey}' and was recorded for manual review.",
                        refund.Id,
                        context.OriginalTransactionId,
                        attempt.ProviderKey);
                }

                return refund;
            }

            await ExecuteRefundAsync(provider, refund, attempt, context, cancellationToken);

            await _refundStore.UpdateAsync(refund, cancellationToken);

            return refund;
        }
    }

    private PaymentRefund BuildRefund(
        RequestPaymentRefundContext context,
        PaymentAttempt attempt,
        decimal maxRefundableGross,
        decimal alreadyRefunded,
        decimal requestedGross)
    {
        var refundTax = 0m;
        var refundTaxable = requestedGross;
        IList<TaxLine> lines = [];

        var calculator = _taxRefundCalculators.FirstOrDefault();

        if (calculator is not null && attempt.TaxSnapshot is not null && attempt.TaxSnapshot.TotalAmount > 0)
        {
            // A full refund of the whole charge reuses the snapshot's captured amounts exactly; a partial
            // refund allocates proportionally. Either way the tax comes from the historical snapshot.
            var isFullRefund = alreadyRefunded == 0m &&
                CurrencyScale.ToMinorUnits(requestedGross, attempt.Currency) == CurrencyScale.ToMinorUnits(maxRefundableGross, attempt.Currency);

            var taxResult = isFullRefund
                ? calculator.CalculateFullRefund(attempt.TaxSnapshot)
                : calculator.CalculateProportionalRefund(attempt.TaxSnapshot, requestedGross);

            refundTax = taxResult.RefundedTaxAmount;
            refundTaxable = taxResult.RefundedTaxableAmount;
            lines = taxResult.Lines ?? [];
        }

        var refund = new PaymentRefund
        {
            Id = IdGenerator.GenerateId(),
            SessionId = context.SessionId,
            ProviderKey = attempt.ProviderKey,
            OriginalAttemptId = attempt.Id,
            OriginalTransactionId = context.OriginalTransactionId,
            ObligationId = attempt.ObligationId,
            Currency = attempt.Currency,
            RefundGrossAmount = requestedGross,
            RefundTaxAmount = refundTax,
            RefundTaxableAmount = refundTaxable,
            TaxLines = lines,
            Reason = context.Reason,
            GatewayMode = attempt.GatewayMode,
            Status = RefundStatus.Requested,
        };

        // The refund id seeds the provider idempotency key so retrying the same refund record never
        // double-refunds at the gateway.
        refund.IdempotencyKey = "refund_" + refund.Id;

        return refund;
    }

    private async Task ExecuteRefundAsync(
        ICheckoutPaymentRefundProvider provider,
        PaymentRefund refund,
        PaymentAttempt attempt,
        RequestPaymentRefundContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.RefundAsync(new RefundPaymentContext
            {
                OriginalTransactionId = context.OriginalTransactionId,
                OriginalProviderReference = attempt.ProviderReference,
                Amount = refund.RefundGrossAmount,
                Currency = refund.Currency,
                IdempotencyKey = refund.IdempotencyKey,
                Reason = refund.Reason,
                GatewayMode = attempt.GatewayMode,
            }, cancellationToken);

            refund.Status = result.Status;
            refund.ProviderRefundReference = result.ProviderRefundReference;
            refund.FailureCode = result.FailureCode;
            refund.FailureReason = result.FailureReason;

            if (result.Status is RefundStatus.Succeeded or RefundStatus.Failed or RefundStatus.Canceled)
            {
                refund.CompletedUtc = _clock.UtcNow;
            }
        }
        catch (Exception ex)
        {
            // The provider mutation is unconfirmed. Leave the refund non-terminal so a later
            // reconciliation or operator can resolve it rather than assuming success or failure.
            refund.Status = RefundStatus.Pending;
            refund.FailureReason = ex.Message;

            _logger.LogError(
                ex,
                "The refund '{RefundId}' for transaction '{TransactionId}' failed to complete against provider '{ProviderKey}' and was left pending.",
                refund.Id,
                context.OriginalTransactionId,
                attempt.ProviderKey);
        }
    }
}
