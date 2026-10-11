using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="ISubscriptionLifecycleService"/>.
/// </summary>
/// <remarks>
/// Every method here takes a distributed lock on the one subscription it changes, re-reads it inside the
/// lock, and refuses transitions that would be wrong rather than applying them. That matters because the
/// callers genuinely do race: a Stripe webhook, the nightly renewal sweep, an operator in the admin, and the
/// customer in their portal can all arrive within the same second.
/// </remarks>
public sealed class DefaultSubscriptionLifecycleService : ISubscriptionLifecycleService
{
    private const string LockPrefix = "SUBSCRIPTION_";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IEnumerable<ISubscriptionLifecycleHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSubscriptionLifecycleService"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="distributedLock">The lock used to serialize transitions of one subscription.</param>
    /// <param name="siteService">The site service used to read the dunning settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="handlers">The handlers notified after a transition.</param>
    /// <param name="logger">The logger.</param>
    public DefaultSubscriptionLifecycleService(
        ISubscriptionManager subscriptionManager,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IClock clock,
        IEnumerable<ISubscriptionLifecycleHandler> handlers,
        ILogger<DefaultSubscriptionLifecycleService> logger)
    {
        _subscriptionManager = subscriptionManager;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _clock = clock;
        _handlers = handlers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Subscription> RecordRenewalAsync(string subscriptionId, DateTime periodStartUtc, SubscriptionRenewalContext context = null, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, subscription =>
        {
            // A webhook and the renewal sweep both report the same cycle. Advancing on the second report
            // would move the customer a whole period ahead and skip a payment.
            if (subscription.CurrentPeriodStartUtc >= periodStartUtc && subscription.CyclesBilled > 0)
            {
                return SubscriptionTransition.NoChange;
            }

            if (subscription.Status is SubscriptionStatus.Expired or SubscriptionStatus.Canceled && _clock.UtcNow > subscription.CurrentPeriodEndUtc)
            {
                // The agreement is over. Recording another cycle against it would resurrect a subscription
                // the customer already left.
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;

            subscription.CyclesBilled++;
            subscription.CurrentPeriodStartUtc = periodStartUtc;
            subscription.CurrentPeriodEndUtc = context?.PeriodEndUtc ?? subscription.Advance(periodStartUtc);
            subscription.PastDueSinceUtc = null;
            subscription.GraceEndsUtc = null;
            subscription.Status = SubscriptionStatus.Active;

            var reachedLimit = subscription.BillingCycleLimit.HasValue && subscription.CyclesBilled >= subscription.BillingCycleLimit.Value;

            // An agreement that reached its agreed number of cycles, or one the customer already asked to
            // end, must stop scheduling a next bill even though this cycle succeeded.
            subscription.NextBillingUtc = reachedLimit || subscription.CancelAtPeriodEnd
                ? null
                : subscription.CurrentPeriodEndUtc;

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.Renewed,
                Source = context?.Source,
                Message = $"Cycle {subscription.CyclesBilled} was billed for the period starting {periodStartUtc:u}.",
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<Subscription> MarkPastDueAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, async subscription =>
        {
            if (subscription.Status is SubscriptionStatus.Expired or SubscriptionStatus.Canceled)
            {
                return SubscriptionTransition.NoChange;
            }

            // A second failure inside the same dunning window must not restart the grace clock, or a
            // customer whose card keeps failing would keep access indefinitely.
            if (subscription.Status == SubscriptionStatus.PastDue && subscription.PastDueSinceUtc.HasValue)
            {
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;
            var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();

            subscription.Status = SubscriptionStatus.PastDue;
            subscription.PastDueSinceUtc = now;
            subscription.GraceEndsUtc = now.AddDays(Math.Max(0, settings.DunningGraceDays));

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.PaymentFailed,
                Message = reason,
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<Subscription> CancelAsync(string subscriptionId, bool atPeriodEnd, string reason, string source = null, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, subscription =>
        {
            if (subscription.Status is SubscriptionStatus.Expired)
            {
                return SubscriptionTransition.NoChange;
            }

            if (subscription.Status == SubscriptionStatus.Canceled && subscription.CancelAtPeriodEnd == atPeriodEnd)
            {
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;

            subscription.Status = SubscriptionStatus.Canceled;
            subscription.CancelAtPeriodEnd = atPeriodEnd;
            subscription.CanceledUtc = now;
            subscription.CancellationReason = reason;

            // Nothing more will be billed either way. What differs is how long access lasts, and that is
            // decided by the period end, not by the status.
            subscription.NextBillingUtc = null;

            if (!atPeriodEnd)
            {
                subscription.CurrentPeriodEndUtc = now;
            }

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.Canceled,
                Source = source,
                Message = atPeriodEnd
                    ? $"Canceled; access runs to {subscription.CurrentPeriodEndUtc:u}. {reason}".Trim()
                    : $"Canceled immediately. {reason}".Trim(),
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken, source);

    /// <inheritdoc/>
    public Task<Subscription> ResumeAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, subscription =>
        {
            if (subscription.Status is not (SubscriptionStatus.PastDue or SubscriptionStatus.Paused))
            {
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;

            subscription.Status = SubscriptionStatus.Active;
            subscription.PastDueSinceUtc = null;
            subscription.GraceEndsUtc = null;
            subscription.NextBillingUtc = subscription.CancelAtPeriodEnd ? null : subscription.CurrentPeriodEndUtc;

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.Resumed,
                Message = "The subscription was returned to active.",
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<Subscription> PauseAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, subscription =>
        {
            if (subscription.Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired or SubscriptionStatus.Paused)
            {
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;

            subscription.Status = SubscriptionStatus.Paused;
            subscription.NextBillingUtc = null;

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.Paused,
                Message = reason,
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<Subscription> ExpireAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(subscriptionId, subscription =>
        {
            if (subscription.Status == SubscriptionStatus.Expired)
            {
                return SubscriptionTransition.NoChange;
            }

            var now = _clock.UtcNow;

            subscription.Status = SubscriptionStatus.Expired;
            subscription.NextBillingUtc = null;
            subscription.GraceEndsUtc = null;

            if (subscription.CurrentPeriodEndUtc > now)
            {
                subscription.CurrentPeriodEndUtc = now;
            }

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = now,
                Type = SubscriptionEventType.Expired,
                Message = reason,
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<Subscription> SyncFromProviderAsync(string subscriptionId, SubscriptionProviderSyncContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return MutateAsync(subscriptionId, subscription =>
        {
            var changed = false;

            if (context.CurrentPeriodEndUtc.HasValue && context.CurrentPeriodEndUtc.Value != subscription.CurrentPeriodEndUtc)
            {
                subscription.CurrentPeriodEndUtc = context.CurrentPeriodEndUtc.Value;

                if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing && !subscription.CancelAtPeriodEnd)
                {
                    subscription.NextBillingUtc = context.CurrentPeriodEndUtc.Value;
                }

                changed = true;
            }

            if (context.CancelAtPeriodEnd.HasValue && context.CancelAtPeriodEnd.Value != subscription.CancelAtPeriodEnd)
            {
                subscription.CancelAtPeriodEnd = context.CancelAtPeriodEnd.Value;

                if (context.CancelAtPeriodEnd.Value)
                {
                    subscription.NextBillingUtc = null;
                }

                changed = true;
            }

            if (context.Status.HasValue && context.Status.Value != subscription.Status)
            {
                // The provider is what actually bills, so its status wins — except where this site has made
                // a decision the provider has no way to express.
                //
                // A cancellation that has not reached the provider yet is one: reviving it here would start
                // billing a customer who already left. A suspension is the other, and it is easier to miss,
                // because a gateway that suspends collection still reports the agreement as active. Letting
                // that answer win would quietly resume a suspension the operator asked for, the moment any
                // unrelated change arrived.
                var localDecision = subscription.Status is SubscriptionStatus.Canceled or SubscriptionStatus.Paused;
                var providerEnded = context.Status.Value is SubscriptionStatus.Canceled or SubscriptionStatus.Expired;

                if (!localDecision || providerEnded)
                {
                    subscription.Status = context.Status.Value;
                    changed = true;
                }
            }

            if (!changed)
            {
                return SubscriptionTransition.NoChange;
            }

            subscription.Events.Add(new SubscriptionEvent
            {
                CreatedUtc = _clock.UtcNow,
                Type = SubscriptionEventType.Changed,
                Source = context.Source,
                Message = "The subscription was synchronized with the payment provider.",
            });

            return SubscriptionTransition.Changed;
        }, cancellationToken, context.Source);
    }

    private Task<Subscription> MutateAsync(string subscriptionId, Func<Subscription, SubscriptionTransition> mutate, CancellationToken cancellationToken, string source = null)
        => MutateAsync(subscriptionId, subscription => Task.FromResult(mutate(subscription)), cancellationToken, source);

    private async Task<Subscription> MutateAsync(string subscriptionId, Func<Subscription, Task<SubscriptionTransition>> mutate, CancellationToken cancellationToken, string source = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + subscriptionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Another node is applying a transition to this very subscription. Returning what is stored is
            // safer than applying a second transition on top of a half-finished one.
            _logger.LogWarning("Could not acquire the lock for subscription '{SubscriptionId}'; the transition was skipped.", subscriptionId);

            return await _subscriptionManager.FindByIdAsync(subscriptionId, cancellationToken);
        }

        await using var _ = locker;

        // Re-read inside the lock: whatever the caller saw may already be stale.
        var subscription = await _subscriptionManager.FindByIdAsync(subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var before = subscription.Status;

        if (await mutate(subscription) == SubscriptionTransition.NoChange)
        {
            return subscription;
        }

        await _subscriptionManager.UpdateAsync(subscription, cancellationToken: cancellationToken);

        await _handlers.InvokeAsync(
            (handler, context) => handler.ChangedAsync(context),
            new SubscriptionLifecycleContext(subscription, before, source),
            _logger);

        return subscription;
    }

    private enum SubscriptionTransition
    {
        NoChange,
        Changed,
    }
}
