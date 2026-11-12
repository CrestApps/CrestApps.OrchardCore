using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Counts a declined card payment for the card-testing protection.
/// </summary>
public interface IPaymentDeclineRecorder
{
    /// <summary>
    /// Counts a payment attempt that has just become <see cref="PaymentAttemptState.Failed"/>. Call it once per
    /// attempt, on the transition (<see cref="PaymentDeclineRecorder.IsNewDecline"/>), never for a replay.
    /// </summary>
    /// <param name="session">The checkout the attempt belongs to.</param>
    /// <param name="attempt">The declined attempt.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IPaymentDeclineRecorder"/>: a decline of a provider that takes cards online counts against the
/// network the payment was begun from, the signed-in customer and the store. The first time one of them is blocked, a
/// payment issue tells the staff.
/// </summary>
public sealed class PaymentDeclineRecorder : IPaymentDeclineRecorder
{
    /// <summary>
    /// The key of the store-wide decline counter.
    /// </summary>
    public const string StoreKey = "store";

    private readonly IAttemptLimiter _limiter;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IPaymentIssueRecorder _issues;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentDeclineRecorder"/> class.
    /// </summary>
    public PaymentDeclineRecorder(
        IAttemptLimiter limiter,
        ICheckoutPaymentProviderResolver providerResolver,
        IPaymentIssueRecorder issues,
        ILogger<PaymentDeclineRecorder> logger)
    {
        _limiter = limiter;
        _providerResolver = providerResolver;
        _issues = issues;
        _logger = logger;
    }

    /// <summary>
    /// Whether a change of state is a new decline: an attempt that was not failed and now is.
    /// </summary>
    /// <param name="previous">The state before.</param>
    /// <param name="current">The state now.</param>
    public static bool IsNewDecline(PaymentAttemptState previous, PaymentAttemptState current)
        => previous != PaymentAttemptState.Failed && current == PaymentAttemptState.Failed;

    /// <inheritdoc/>
    public async Task RecordAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(attempt);

        if (_providerResolver.GetProvider(attempt.ProviderKey)?.Capabilities?.IsCardNotPresent != true)
        {
            return;
        }

        try
        {
            if (session.TryGet<CheckoutClientInfo>(out var client) && !string.IsNullOrEmpty(client.NetworkKey))
            {
                await CountAsync(
                    CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork,
                    client.NetworkKey,
                    session,
                    attempt,
                    PaymentIssueKinds.SuspectedCardTesting,
                    "Card payments from one network were declined until the store stopped taking card payments from it for a while. Someone may be testing stolen cards.",
                    cancellationToken);
            }

            if (!string.IsNullOrEmpty(session.OwnerId))
            {
                await CountAsync(
                    CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer,
                    session.OwnerId,
                    session,
                    attempt,
                    PaymentIssueKinds.SuspectedCardTesting,
                    "A customer's card payments were declined until the store stopped taking card payments from them for a while. Their account may be used to test stolen cards.",
                    cancellationToken);
            }

            await CountAsync(
                CheckoutConstants.AttemptPolicies.PaymentDeclineStore,
                StoreKey,
                session,
                attempt,
                PaymentIssueKinds.DeclineSpike,
                "Card payments are being declined across the store far more often than usual, so the store's decline-spike action applies for a while. Check the payment provider's dashboard for card testing.",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Counting a decline must never break the payment flow that reported it.
            _logger.LogError(exception, "The decline of payment attempt '{AttemptId}' could not be counted.", attempt.ItemId);
        }
    }

    private async Task CountAsync(string policy, string key, CheckoutSession session, PaymentAttempt attempt, string kind, string message, CancellationToken cancellationToken)
    {
        var before = await _limiter.PeekAsync(policy, key, cancellationToken);
        var after = await _limiter.RecordFailureAsync(policy, key, cancellationToken);

        // Only the decline that blocks the key records an issue; the ones after it, while it is blocked, don't.
        if (!before.Allowed || after.Allowed)
        {
            return;
        }

        await _issues.RecordAsync(new PaymentIssue
        {
            Kind = kind,
            Message = message,
            ReferenceType = CheckoutReferenceType,
            ReferenceId = session.SessionId,
            ProviderKey = attempt.ProviderKey,
            ProviderReference = attempt.ProviderReference,
            Amount = attempt.ExpectedAmount,
            Currency = session.Currency,
        }, cancellationToken);
    }

    private const string CheckoutReferenceType = "CheckoutSession";
}
