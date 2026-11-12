using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Counts a declined card payment for the card-testing protection.
/// </summary>
public interface IPaymentDeclineRecorder
{
    /// <summary>
    /// Counts one declined card of a payment attempt. Call it once per decline: on the attempt's transition to
    /// <see cref="PaymentAttemptState.Failed"/> (<see cref="PaymentDeclineRecorder.IsNewDecline"/>), or once per failure
    /// event a provider sends while the attempt stays open, never for a replay. A saved card charged without the payer (a
    /// renewal, a staff charge) is not someone trying cards and is never passed here: a batch of renewals that decline must
    /// not block the customer or raise a store-wide decline spike.
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
    private readonly AttemptLimitOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentDeclineRecorder"/> class.
    /// </summary>
    public PaymentDeclineRecorder(
        IAttemptLimiter limiter,
        ICheckoutPaymentProviderResolver providerResolver,
        IPaymentIssueRecorder issues,
        IOptions<AttemptLimitOptions> options,
        ILogger<PaymentDeclineRecorder> logger)
    {
        _limiter = limiter;
        _providerResolver = providerResolver;
        _issues = issues;
        _options = options.Value;
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
        // Only the decline that blocks the key records an issue, even when declines arrive in parallel: the limiter says
        // which one used up the window, under its lock. A refusal because the counter couldn't be reached is not a block.
        var result = await _limiter.RecordFailureAsync(policy, key, cancellationToken);

        if (!result.ReachedLimit)
        {
            return;
        }

        var limit = _options.Policies.TryGetValue(policy, out var registered) ? registered : null;
        var who = Describe(policy, key);

        _logger.LogWarning("Card payments from {Who} were blocked by the attempt policy '{Policy}' after {Count} declines.", who, policy, limit?.PermitLimit);

        await _issues.RecordAsync(new PaymentIssue
        {
            Kind = kind,
            Message = message,
            Error = limit is null
                ? who
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{who}: {limit.PermitLimit} declines within {limit.Window}, blocked for {limit.BlockDuration}."),
            ReferenceType = CheckoutReferenceType,
            ReferenceId = session.SessionId,
            ProviderKey = attempt.ProviderKey,
            ProviderReference = attempt.ProviderReference,
            Amount = attempt.ExpectedAmount,
            Currency = session.Currency,
        }, cancellationToken);
    }

    // Who was blocked, as staff may read it: a network masked to /24 (an IPv6 key is a /64 already), a customer, or the
    // store.
    internal static string Describe(string policy, string key)
    {
        if (string.Equals(policy, CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, StringComparison.Ordinal))
        {
            return "network " + MaskNetwork(key);
        }

        return string.Equals(policy, CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, StringComparison.Ordinal)
            ? "customer " + key
            : "the store";
    }

    internal static string MaskNetwork(string networkKey)
    {
        if (System.Net.IPAddress.TryParse(networkKey, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            bytes[3] = 0;

            return new System.Net.IPAddress(bytes) + "/24";
        }

        return networkKey;
    }
    private const string CheckoutReferenceType = "CheckoutSession";
}
