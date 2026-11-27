using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <inheritdoc/>
public sealed class CheckoutPaymentHoldService : ICheckoutPaymentHoldService
{
    private readonly IPaymentAttemptStore _attempts;
    private readonly ICheckoutSessionStore _sessions;
    private readonly ICheckoutPaymentReleaseService _release;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentHoldService"/> class.
    /// </summary>
    /// <param name="attempts">The payment attempts, which tell whether the holder's payment may still take the money.</param>
    /// <param name="sessions">The checkout sessions, which tell whether the holder's checkout is finished.</param>
    /// <param name="release">Cancels the holder's payment once it held the record too long and took nothing.</param>
    /// <param name="distributedLock">The lock a checkout's begin holds while it talks to the provider.</param>
    /// <param name="clock">The clock.</param>
    public CheckoutPaymentHoldService(
        IPaymentAttemptStore attempts,
        ICheckoutSessionStore sessions,
        ICheckoutPaymentReleaseService release,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _attempts = attempts;
        _sessions = sessions;
        _release = release;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> IsHeldByAnotherAsync(CheckoutPaymentHold hold, string sessionId, Func<PaymentAttempt, bool> isApplied = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hold?.SessionId) ||
            string.Equals(hold.SessionId, sessionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (await _sessions.GetAsync(hold.SessionId, cancellationToken) is not { } holder ||
            holder.Status is CheckoutSessionStatus.Completed or CheckoutSessionStatus.Failed or CheckoutSessionStatus.Canceled or CheckoutSessionStatus.Expired)
        {
            return false;
        }

        var attempts = (await _attempts.GetBySessionAsync(hold.SessionId, cancellationToken)).ToList();

        // Let through the gate, its payment hasn't reached the provider yet, or didn't and never will. A begin still
        // talking to the provider holds its checkout's lock, so one slower than the window still holds the record.
        if (attempts.Count == 0)
        {
            return _clock.UtcNow - hold.HeldUtc < CheckoutPaymentHold.BeginWindow ||
                await _distributedLock.IsLockAcquiredAsync(CheckoutConstants.SessionLockPrefix + hold.SessionId);
        }

        if (attempts.Any(attempt => attempt.State == PaymentAttemptState.Succeeded && isApplied?.Invoke(attempt) != true))
        {
            return true;
        }

        if (!attempts.Any(attempt => attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending))
        {
            return false;
        }

        // A payment begun and not finished holds the record for a while; after that it is canceled at the provider,
        // which refuses when the money was taken or is being taken, and the record is free again.
        return _clock.UtcNow - hold.HeldUtc < CheckoutPaymentHold.PendingHoldLimit ||
            await _release.ReleaseAsync(hold.SessionId, cancellationToken) != CheckoutPaymentReleaseResult.Released;
    }

    /// <inheritdoc/>
    public CheckoutPaymentHold Take(CheckoutPaymentHold current, string sessionId)
        => new()
        {
            SessionId = sessionId,
            HeldUtc = current is not null && string.Equals(current.SessionId, sessionId, StringComparison.Ordinal)
                ? current.HeldUtc
                : _clock.UtcNow,
        };
}
