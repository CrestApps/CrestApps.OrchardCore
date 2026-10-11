using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Grants and takes back what a subscription entitles its owner to, whenever the subscription's state
/// changes.
/// </summary>
/// <remarks>
/// Without this, a subscription is only a billing record: somebody pays and nothing about the site changes,
/// and somebody stops paying and keeps everything. What makes it act at the right moment is that access is
/// decided by whether the agreement is <em>current</em>, not by its status. A customer who cancels mid-cycle
/// keeps what they paid for, and one whose card failed last night keeps it through the grace window; only
/// when the agreement genuinely stops being current is anything taken away.
///
/// A failing applier is logged and does not roll the transition back. The agreement's state is the fact; a
/// role that could not be removed is a side effect to retry, not a reason to pretend the subscription is
/// still active.
/// </remarks>
public sealed class EntitlementSubscriptionLifecycleHandler : SubscriptionLifecycleHandlerBase
{
    private readonly IEnumerable<ISubscriptionEntitlementApplier> _appliers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntitlementSubscriptionLifecycleHandler"/> class.
    /// </summary>
    /// <param name="appliers">The registered entitlement appliers.</param>
    /// <param name="clock">The clock used to decide whether the agreement is current.</param>
    /// <param name="logger">The logger.</param>
    public EntitlementSubscriptionLifecycleHandler(
        IEnumerable<ISubscriptionEntitlementApplier> appliers,
        IClock clock,
        ILogger<EntitlementSubscriptionLifecycleHandler> logger)
    {
        _appliers = appliers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ChangedAsync(SubscriptionLifecycleContext context)
    {
        var subscription = context?.Subscription;

        if (subscription is null || subscription.Entitlements.Count == 0)
        {
            return;
        }

        var isCurrent = subscription.IsCurrent(_clock.UtcNow);

        foreach (var entitlement in subscription.Entitlements)
        {
            var applier = Resolve(entitlement.Kind);

            if (applier is null)
            {
                // The feature that understands this kind is not enabled on the tenant. That is a
                // configuration choice, not an error, so nothing is granted and nothing is logged loudly.
                continue;
            }

            var applierContext = new SubscriptionEntitlementContext(subscription, entitlement);

            try
            {
                if (isCurrent)
                {
                    await applier.ApplyAsync(applierContext);
                }
                else
                {
                    await applier.RevokeAsync(applierContext);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to {Action} the '{Kind}' entitlement for subscription '{SubscriptionId}'.",
                    isCurrent ? "apply" : "revoke",
                    entitlement.Kind,
                    subscription.ItemId);
            }
        }
    }

    private ISubscriptionEntitlementApplier Resolve(string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return null;
        }

        foreach (var applier in _appliers)
        {
            if (string.Equals(applier.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                return applier;
            }
        }

        return null;
    }
}
