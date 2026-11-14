using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// What became of the payments begun for a checkout that is about to be edited.
/// </summary>
public enum CheckoutPaymentReleaseResult
{
    /// <summary>
    /// No payment is live any more: the edit may go ahead.
    /// </summary>
    Released,

    /// <summary>
    /// A payment was taken already, or the provider would not cancel it: the edit must wait.
    /// </summary>
    PaymentInProgress,
}

/// <summary>
/// Sets aside the payments begun for a checkout before it is edited. A payment is begun for the total of the moment; a
/// buyer who then changes shipping, a code or a balance must pay the new total, so the old payment is canceled at the
/// provider and superseded, which kills its client secret.
/// </summary>
public interface ICheckoutPaymentReleaseService
{
    /// <summary>
    /// Cancels and supersedes every live payment of the checkout, under the checkout's lock.
    /// </summary>
    /// <param name="sessionId">The checkout session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the edit may go ahead.</returns>
    Task<CheckoutPaymentReleaseResult> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
public sealed class CheckoutPaymentReleaseService : ICheckoutPaymentReleaseService
{
    private const string Reason = "The checkout changed before this payment was finished.";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentReleaseService"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="attemptStore">The payment attempt ledger.</param>
    /// <param name="providerResolver">The payment provider resolver.</param>
    /// <param name="distributedLock">The lock the checkout engine also takes for the session.</param>
    /// <param name="session">The YesSql session, committed while the lock is held.</param>
    /// <param name="logger">The logger.</param>
    public CheckoutPaymentReleaseService(
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptStore attemptStore,
        ICheckoutPaymentProviderResolver providerResolver,
        IDistributedLock distributedLock,
        ISession session,
        ILogger<CheckoutPaymentReleaseService> logger)
    {
        _sessionStore = sessionStore;
        _attemptStore = attemptStore;
        _providerResolver = providerResolver;
        _distributedLock = distributedLock;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CheckoutPaymentReleaseResult> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var live = (await _attemptStore.GetBySessionAsync(sessionId, cancellationToken))
            .Where(IsLive)
            .ToList();

        if (live.Count == 0)
        {
            return CheckoutPaymentReleaseResult.Released;
        }

        // The same lock the engine takes to begin or complete a payment, so a payment can't be begun or settled while it
        // is being set aside.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultCheckoutEngine.SessionLockPrefix + sessionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return CheckoutPaymentReleaseResult.PaymentInProgress;
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            foreach (var attempt in (await _attemptStore.GetBySessionAsync(sessionId, cancellationToken)).Where(IsLive))
            {
                if (!await TryReleaseAsync(session, attempt, cancellationToken))
                {
                    await _session.SaveChangesAsync(cancellationToken);

                    return CheckoutPaymentReleaseResult.PaymentInProgress;
                }
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        return CheckoutPaymentReleaseResult.Released;
    }

    private static bool IsLive(PaymentAttempt attempt)
        => attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending && !attempt.IsSuperseded;

    // A payment the provider says is taken is never dropped, and one the provider won't cancel (it is processing) is left
    // to finish; either refuses the edit. Otherwise it is canceled at the provider and superseded.
    private async Task<bool> TryReleaseAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (_providerResolver.GetProvider(attempt.ProviderKey) is not { } provider)
        {
            return false;
        }

        try
        {
            // A provider that defers payment (Pay Later, a bank transfer, cash on delivery) says "succeeded" as soon as it is
            // begun, though no money moved; asking it would refuse every edit after such a begin, for good. It is set aside.
            if (!string.IsNullOrEmpty(attempt.ProviderReference) && provider.Capabilities?.DefersPayment != true)
            {
                var verification = await provider.VerifyAsync(new VerifyPaymentContext { Session = session, Attempt = attempt }, cancellationToken);

                if (verification.Status == PaymentStatus.Succeeded)
                {
                    return false;
                }
            }

            var canceled = await provider.CancelAsync(new CancelPaymentContext { Session = session, Attempt = attempt, Reason = Reason }, cancellationToken);

            if (!canceled.Succeeded)
            {
                return false;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Attempt '{AttemptId}' of checkout '{SessionId}' could not be set aside before the checkout was edited.", attempt.ItemId, attempt.SessionId);

            return false;
        }

        attempt.State = PaymentAttemptState.Canceled;
        attempt.IsSuperseded = true;
        attempt.FailureReason = Reason;

        await _attemptStore.UpdateAsync(attempt, cancellationToken);

        return true;
    }
}
