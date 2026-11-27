using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Tells an agreement this site bills itself from one a payment gateway bills on its own schedule.
/// </summary>
/// <remarks>
/// A provider that defers payment (Pay Later, a bank transfer, cash on delivery) records a reference for every agreement
/// it takes, but that reference names no schedule at a gateway: this site invoices each cycle, and a payment recorded
/// here is the only payment there is. So the reference alone does not make an agreement gateway-billed; the provider's
/// <see cref="PaymentProviderCapabilities.DefersPayment"/> capability decides.
/// </remarks>
public static class OfflineSubscriptionBilling
{
    /// <summary>
    /// Whether this site bills <paramref name="subscription"/> itself: it has no gateway reference, or its provider defers
    /// payment.
    /// </summary>
    /// <param name="subscription">The agreement.</param>
    /// <param name="providerResolver">The payment providers.</param>
    /// <returns><see langword="true"/> when the agreement is billed offline.</returns>
    public static bool IsOffline(Subscription subscription, ICheckoutPaymentProviderResolver providerResolver)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        if (string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            return true;
        }

        return providerResolver is not null &&
            !string.IsNullOrEmpty(subscription.ProviderKey) &&
            providerResolver.GetProvider(subscription.ProviderKey)?.Capabilities?.DefersPayment == true;
    }
}
