using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Counts the declines a provider reports by webhook. A provider that confirms the card in the browser, such as Stripe,
/// begins the payment with a client secret and the card is declined at the provider, so neither the begin nor the
/// reconciliation sweep sees it: the buyer can try card after card against one secret. Each failure counts for the
/// card-testing protection. After a few on one payment, or once the network or customer it was begun from is blocked, the
/// payment is canceled at the provider and the attempt failed, so the buyer must begin a new payment, which the gate can
/// refuse. A subscription's first payment counts the same way, through the failed first invoice.
/// </summary>
public sealed class CardDeclinePaymentEventHandler : PaymentEventBase
{
    /// <summary>
    /// The metadata key a provider sends the checkout attempt's id back in.
    /// </summary>
    public const string AttemptIdDataKey = "checkout_attempt_id";

    private const string Reason = "Too many cards were declined for this payment.";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentDeclineRecorder _declineRecorder;
    private readonly IAttemptLimiter _limiter;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardDeclinePaymentEventHandler"/> class.
    /// </summary>
    public CardDeclinePaymentEventHandler(
        IPaymentAttemptStore attemptStore,
        ICheckoutSessionStore sessionStore,
        IPaymentDeclineRecorder declineRecorder,
        IAttemptLimiter limiter,
        ICheckoutPaymentProviderResolver providerResolver,
        IDistributedLock distributedLock,
        ILogger<CardDeclinePaymentEventHandler> logger)
    {
        _attemptStore = attemptStore;
        _sessionStore = sessionStore;
        _declineRecorder = declineRecorder;
        _limiter = limiter;
        _providerResolver = providerResolver;
        _distributedLock = distributedLock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task PaymentFailedAsync(PaymentFailedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DeclinedAsync(context, context.TransactionId, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task SubscriptionPaymentFailedAsync(SubscriptionPaymentFailedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A subscription checkout's attempt holds the subscription, and its first invoice's declines come as a subscription
        // payment failure carrying the subscription's metadata. A renewal's attempt finished long ago and counts nothing.
        return DeclinedAsync(context, context.SubscriptionId, cancellationToken);
    }

    private async Task DeclinedAsync(PaymentEventContextBase context, string providerReference, CancellationToken cancellationToken)
    {
        if (!context.Data.TryGetValue(AttemptIdDataKey, out var value) || value?.ToString() is not { Length: > 0 } attemptId)
        {
            return;
        }

        var attempt = await _attemptStore.FindByIdAsync(attemptId, cancellationToken);

        // Only a live attempt of this provider's payment: a stale or replayed event about a finished one counts nothing.
        if (!IsLive(attempt, providerReference))
        {
            return;
        }

        var sessions = await _sessionStore.GetManyAsync([attempt.SessionId], cancellationToken);
        var session = sessions.Count > 0 ? sessions[0] : null;

        if (session is null)
        {
            return;
        }

        // The counters live in the cache, not in the transaction the webhook commits: an event the provider delivers again,
        // because a later handler failed, must not count the same decline twice. A redelivery may still cancel below.
        if (await IsFirstDeliveryAsync(context.EventId, cancellationToken))
        {
            await _declineRecorder.RecordAsync(session, attempt, cancellationToken);
            await _limiter.RecordFailureAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt, attempt.ItemId, cancellationToken);
        }

        if (await MayTakeAnotherCardAsync(session, attempt, cancellationToken))
        {
            return;
        }

        await CancelAsync(session, attempt.ItemId, providerReference, cancellationToken);
    }

    private static bool IsLive(PaymentAttempt attempt, string providerReference)
        => attempt is not null &&
            !attempt.IsSuperseded &&
            attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending &&
            (string.IsNullOrEmpty(attempt.ProviderReference) || string.IsNullOrEmpty(providerReference) || string.Equals(attempt.ProviderReference, providerReference, StringComparison.Ordinal));

    private async Task<bool> IsFirstDeliveryAsync(string eventId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(eventId) ||
            (await _limiter.AcquireAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineEvent, eventId, cancellationToken)).Allowed;

    // Whether the payment may stay open for another card. The counters are read, not counted: a counter whose lock could
    // not be taken answers "refused" when counting, and that is no reason to cancel a buyer's payment after one decline.
    // Each block is checked here as well as at begin, so a network or customer that is blocked can't keep trying cards
    // against the client secrets of payments it began before.
    private async Task<bool> MayTakeAnotherCardAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (!(await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt, attempt.ItemId, cancellationToken)).Allowed)
        {
            return false;
        }

        if (session.TryGet<CheckoutClientInfo>(out var client) && !string.IsNullOrEmpty(client.NetworkKey) &&
            !(await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, client.NetworkKey, cancellationToken)).Allowed)
        {
            return false;
        }

        return string.IsNullOrEmpty(session.OwnerId) ||
            (await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, session.OwnerId, cancellationToken)).Allowed;
    }

    // Under the checkout's lock, the one the engine takes to complete a payment, so a payment being completed is not failed
    // at the same time. The provider cancels only a payment that took nothing: one the buyer paid with a later card, whose
    // success was not processed yet, is left paid and never refunded from here.
    private async Task CancelAsync(CheckoutSession session, string attemptId, string providerReference, CancellationToken cancellationToken)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultCheckoutEngine.SessionLockPrefix + session.SessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("Payment attempt '{AttemptId}' had too many declined cards but its checkout was busy; the next decline cancels it.", attemptId);

            return;
        }

        await using (locker)
        {
            var attempt = await _attemptStore.FindByIdAsync(attemptId, cancellationToken);

            if (!IsLive(attempt, providerReference) || _providerResolver.GetProvider(attempt.ProviderKey) is not { } provider)
            {
                return;
            }

            try
            {
                var canceled = await provider.CancelAsync(new CancelPaymentContext { Session = session, Attempt = attempt, Reason = Reason, OnlyIfUnpaid = true }, cancellationToken);

                if (!canceled.Succeeded)
                {
                    _logger.LogWarning("Payment attempt '{AttemptId}' had too many declined cards but the provider would not cancel it: {Error}", attempt.ItemId, canceled.ErrorMessage);

                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Payment attempt '{AttemptId}' had too many declined cards and could not be canceled at the provider.", attempt.ItemId);

                return;
            }

            attempt.State = PaymentAttemptState.Failed;
            attempt.FailureReason = Reason;

            await _attemptStore.UpdateAsync(attempt, cancellationToken);

            _logger.LogWarning("Payment attempt '{AttemptId}' of checkout '{SessionId}' was canceled after too many declined cards.", attempt.ItemId, attempt.SessionId);
        }
    }
}
