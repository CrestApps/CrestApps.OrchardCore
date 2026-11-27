using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Gives up a site that waits for its payment once the subscription it was bought with ends, so the name, prefix and hosts
/// held for it are free again and a payment that arrives later does not build it.
/// </summary>
public sealed class TenantProvisioningSubscriptionEndedHandler : SubscriptionLifecycleHandlerBase
{
    private readonly TenantProvisioningPaymentRelease _release;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningSubscriptionEndedHandler"/> class.
    /// </summary>
    /// <param name="release">The service that releases, or gives up, a site waiting for its payment.</param>
    public TenantProvisioningSubscriptionEndedHandler(TenantProvisioningPaymentRelease release)
    {
        _release = release;
    }

    /// <inheritdoc/>
    public override Task ChangedAsync(SubscriptionLifecycleContext context)
    {
        if (context?.Subscription is not { } subscription ||
            !context.StatusChanged ||
            subscription.Status is not (SubscriptionStatus.Expired or SubscriptionStatus.Canceled))
        {
            return Task.CompletedTask;
        }

        return _release.AbandonEndedAsync(subscription);
    }
}
