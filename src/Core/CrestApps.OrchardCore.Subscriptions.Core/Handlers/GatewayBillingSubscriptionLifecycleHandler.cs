using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Tells the payment provider to stop, suspend, or resume billing when an agreement changes state here.
/// </summary>
/// <remarks>
/// Canceling or pausing only changes what this site believes. The gateway holds the customer's card and its
/// own schedule, and it keeps charging until it is told otherwise — so without this the subscriber sees
/// their subscription end, loses access at the end of the period, and still gets billed every month
/// afterwards; and an operator who suspends billing is told "billing was suspended" while the customer keeps
/// being charged.
///
/// A change the gateway itself reported is deliberately not sent back to it. That transition arrives with
/// the gateway named as its source, and echoing it would ask the provider to cancel an agreement it has
/// already canceled.
/// </remarks>
public sealed class GatewayBillingSubscriptionLifecycleHandler : SubscriptionLifecycleHandlerBase
{
    private readonly IEnumerable<ICheckoutRecurringPaymentProvider> _providers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayBillingSubscriptionLifecycleHandler"/> class.
    /// </summary>
    /// <param name="providers">The recurring payment providers.</param>
    /// <param name="logger">The logger.</param>
    public GatewayBillingSubscriptionLifecycleHandler(
        IEnumerable<ICheckoutRecurringPaymentProvider> providers,
        ILogger<GatewayBillingSubscriptionLifecycleHandler> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ChangedAsync(SubscriptionLifecycleContext context)
    {
        var subscription = context?.Subscription;

        // Only a transition is worth acting on. A later change to an agreement already in the same state —
        // a date correction, a note — must not ask the gateway to do the same thing a second time.
        if (subscription is null || !context.StatusChanged)
        {
            return;
        }

        if (string.IsNullOrEmpty(subscription.ProviderKey) || string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            return;
        }

        // The gateway told us. Sending it back would be an echo, not an instruction.
        if (!string.IsNullOrEmpty(context.Source) &&
            string.Equals(context.Source, subscription.ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Resuming is only meaningful when the agreement was actually suspended here. Returning a past-due
        // agreement to active is a dunning decision, and the gateway is already retrying that one itself.
        var resuming = subscription.Status == SubscriptionStatus.Active &&
            context.PreviousStatus == SubscriptionStatus.Paused;

        if (subscription.Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.Paused) && !resuming)
        {
            return;
        }

        var provider = _providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, subscription.ProviderKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            _logger.LogWarning(
                "Subscription '{SubscriptionId}' changed to '{Status}' but its provider '{ProviderKey}' is not available, so the agreement may still be billed.",
                subscription.ItemId, subscription.Status, subscription.ProviderKey);

            return;
        }

        try
        {
            if (subscription.Status != SubscriptionStatus.Canceled)
            {
                var paused = await provider.PauseRecurringAsync(new PauseRecurringPaymentContext
                {
                    ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                    Paused = subscription.Status == SubscriptionStatus.Paused,
                    Reason = subscription.CancellationReason,
                }, CancellationToken.None);

                if (!paused.Succeeded)
                {
                    _logger.LogError(
                        "Provider '{ProviderKey}' refused to change collection on subscription '{ProviderSubscriptionId}': {Error}. The customer may still be billed.",
                        subscription.ProviderKey, subscription.ProviderSubscriptionId, paused.ErrorMessage);
                }

                return;
            }

            var result = await provider.CancelRecurringAsync(new CancelRecurringPaymentContext
            {
                ProviderSubscriptionId = subscription.ProviderSubscriptionId,

                // Honor the period the customer already paid for: at-period-end here means the same at the
                // gateway, which keeps access and billing telling the same story.
                AtPeriodEnd = subscription.CancelAtPeriodEnd,
                Reason = subscription.CancellationReason,
            });

            if (!result.Succeeded)
            {
                // Deliberately not rethrown: the agreement is canceled here either way, and a handler that
                // throws would only be logged. Surfacing it loudly is what lets an operator stop a customer
                // from being billed for something they canceled.
                _logger.LogError(
                    "Provider '{ProviderKey}' refused to cancel subscription '{ProviderSubscriptionId}': {Error}. The customer may still be billed.",
                    subscription.ProviderKey, subscription.ProviderSubscriptionId, result.ErrorMessage);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to apply the '{Status}' transition of subscription '{ProviderSubscriptionId}' at provider '{ProviderKey}'. The customer may still be billed.",
                subscription.Status, subscription.ProviderSubscriptionId, subscription.ProviderKey);
        }
    }
}
