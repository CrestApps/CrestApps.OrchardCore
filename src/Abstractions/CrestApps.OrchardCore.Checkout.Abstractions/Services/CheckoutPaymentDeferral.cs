using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Whether a completed checkout was paid by a promise rather than by money: a payment method that defers payment (Pay
/// Later, a bank transfer, cash on delivery) completes the checkout with nothing collected. Whatever delivers instantly
/// (a download, a membership, a site) waits for the money in that case.
/// </summary>
public static class CheckoutPaymentDeferral
{
    /// <summary>
    /// Whether any payment that settled the checkout came from a provider that defers payment.
    /// </summary>
    /// <param name="session">The completed checkout.</param>
    /// <param name="providerResolver">The payment provider resolver.</param>
    public static bool IsDeferred(ICheckoutFlowSession session, ICheckoutPaymentProviderResolver providerResolver)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(providerResolver);

        if (!session.TryGet<PaymentsMetadata>(out var metadata) || metadata.Payments is not { Count: > 0 })
        {
            return false;
        }

        return metadata.Payments.Values.Any(payment =>
            !string.IsNullOrEmpty(payment?.GatewayId) &&
            providerResolver.GetProvider(payment.GatewayId)?.Capabilities?.DefersPayment == true);
    }
}
