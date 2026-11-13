using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Counts the declines a provider reports by webhook. A provider that confirms the card in the browser, such as Stripe,
/// begins the payment with a client secret and the card is declined at the provider, so neither the begin nor the
/// reconciliation sweep sees it: the buyer can try card after card against one secret. Each failure counts for the
/// card-testing protection, and after a few the payment is canceled at the provider and the attempt failed, so the buyer
/// must begin a new payment, which the gate can refuse. The webhook runs each provider event once.
/// </summary>
public sealed class CardDeclinePaymentEventHandler : PaymentEventBase
{
    /// <summary>
    /// The metadata key a provider sends the checkout attempt's id back in.
    /// </summary>
    public const string AttemptIdDataKey = "checkout_attempt_id";

    private const string Reason = "Too many cards were declined for this payment.";

    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentDeclineRecorder _declineRecorder;
    private readonly IAttemptLimiter _limiter;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
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
        ILogger<CardDeclinePaymentEventHandler> logger)
    {
        _attemptStore = attemptStore;
        _sessionStore = sessionStore;
        _declineRecorder = declineRecorder;
        _limiter = limiter;
        _providerResolver = providerResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task PaymentFailedAsync(PaymentFailedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Data.TryGetValue(AttemptIdDataKey, out var value) || value?.ToString() is not { Length: > 0 } attemptId)
        {
            return;
        }

        var attempt = await _attemptStore.FindByIdAsync(attemptId, cancellationToken);

        // Only a live attempt of this provider's payment: a stale or replayed event about a finished one counts nothing.
        if (attempt is null ||
            attempt.IsSuperseded ||
            attempt.State is not (PaymentAttemptState.Created or PaymentAttemptState.Pending) ||
            (!string.IsNullOrEmpty(attempt.ProviderReference) && !string.IsNullOrEmpty(context.TransactionId) && !string.Equals(attempt.ProviderReference, context.TransactionId, StringComparison.Ordinal)))
        {
            return;
        }

        var sessions = await _sessionStore.GetManyAsync([attempt.SessionId], cancellationToken);
        var session = sessions.Count > 0 ? sessions[0] : null;

        if (session is null)
        {
            return;
        }

        await _declineRecorder.RecordAsync(session, attempt, cancellationToken);

        if ((await _limiter.RecordFailureAsync(CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt, attempt.ItemId, cancellationToken)).Allowed)
        {
            return;
        }

        if (_providerResolver.GetProvider(attempt.ProviderKey) is not { } provider)
        {
            return;
        }

        try
        {
            var canceled = await provider.CancelAsync(new CancelPaymentContext { Session = session, Attempt = attempt, Reason = Reason }, cancellationToken);

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
