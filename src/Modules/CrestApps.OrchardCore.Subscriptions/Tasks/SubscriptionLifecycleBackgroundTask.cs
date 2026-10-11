using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Tasks;

/// <summary>
/// Moves subscriptions forward on time rather than only when someone happens to look at them.
/// </summary>
/// <remarks>
/// Two things go wrong without this sweep. An agreement whose renewal payment failed stays past due
/// forever, so a customer who never paid keeps their access indefinitely. And an agreement that reached the
/// number of cycles the customer agreed to keeps a next-billing date, so it looks due to bill something it
/// must not.
///
/// The sweep deliberately does not charge anyone. A gateway-backed agreement is billed by the gateway, and
/// an offline one is invoiced by its own provider's renewal path. What happens here is only the passage of
/// time being applied to state.
/// </remarks>
[BackgroundTask(
    Title = "Subscription Lifecycle",
    Schedule = "*/30 * * * *",
    Description = "Expires subscriptions whose dunning grace period elapsed and closes agreements that reached their agreed number of cycles.",
    LockTimeout = 10_000,
    LockExpiration = 120_000)]
public sealed class SubscriptionLifecycleBackgroundTask : IBackgroundTask
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionLifecycleBackgroundTask"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SubscriptionLifecycleBackgroundTask(ILogger<SubscriptionLifecycleBackgroundTask> logger)
        => _logger = logger;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var subscriptionManager = serviceProvider.GetRequiredService<ISubscriptionManager>();
        var lifecycleService = serviceProvider.GetRequiredService<ISubscriptionLifecycleService>();
        var providerResolver = serviceProvider.GetRequiredService<ICheckoutPaymentProviderResolver>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        await ExpireLapsedAsync(subscriptionManager, lifecycleService, now, cancellationToken);
        await RenewDueAsync(subscriptionManager, lifecycleService, providerResolver, now, cancellationToken);
    }

    // A past-due agreement whose grace window ran out is over. Leaving it past due would give away the plan.
    private async Task ExpireLapsedAsync(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var lapsed = await subscriptionManager.GetLapsedAsync(now, cancellationToken);

        foreach (var subscription in lapsed)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await lifecycleService.ExpireAsync(
                    subscription.ItemId,
                    "The grace period for the unpaid renewal elapsed.",
                    cancellationToken);
            }
            catch (Exception exception)
            {
                // One bad agreement must not stop the sweep; it stays lapsed and is retried next run.
                _logger.LogError(exception, "Failed to expire lapsed subscription '{SubscriptionId}'.", subscription.ItemId);
            }
        }
    }

    // Walks every agreement whose next billing date has passed and does what its provider cannot.
    private async Task RenewDueAsync(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        ICheckoutPaymentProviderResolver providerResolver,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var due = await subscriptionManager.GetDueForRenewalAsync(now, cancellationToken);

        foreach (var subscription in due)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var reachedLimit = subscription.BillingCycleLimit.HasValue && subscription.CyclesBilled >= subscription.BillingCycleLimit.Value;

            try
            {
                if (reachedLimit || subscription.CancelAtPeriodEnd)
                {
                    // An agreement that has billed every cycle the customer agreed to is finished, even
                    // though its period has not run out yet. Closing it stops it from appearing as an open
                    // commitment forever.
                    await lifecycleService.ExpireAsync(
                        subscription.ItemId,
                        reachedLimit
                            ? "The subscription reached the number of billing cycles it was sold for."
                            : "The subscription was set to end at the close of the paid period.",
                        cancellationToken);

                    continue;
                }

                var provider = providerResolver.GetProvider(subscription.ProviderKey);

                if (provider is not null && CheckoutPaymentMethodSelector.IsGateway(provider))
                {
                    // A gateway owns this schedule and reports each cycle through its webhook. Advancing it
                    // here would claim a payment the gateway has not confirmed.
                    continue;
                }

                // Nobody else will move an offline agreement: the provider records the next cycle's debt, and
                // this is what records that the cycle began. Without it the agreement would sit at its first
                // period forever while the customer kept being invoiced.
                await lifecycleService.RecordRenewalAsync(
                    subscription.ItemId,
                    subscription.NextBillingUtc ?? subscription.CurrentPeriodEndUtc,
                    new SubscriptionRenewalContext { AmountPaid = null },
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to advance due subscription '{SubscriptionId}'.", subscription.ItemId);
            }
        }
    }
}
