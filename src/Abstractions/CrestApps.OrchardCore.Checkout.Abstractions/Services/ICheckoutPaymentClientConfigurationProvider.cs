using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// What a client collecting a payment itself needs from one payment provider, such as the publishable key a phone app
/// starts Stripe's SDK with. The checkout page gets these from the provider's panel; the commerce API lists them with
/// each payment method. Only values safe to hand any buyer belong here: never a secret key.
/// </summary>
public interface ICheckoutPaymentClientConfigurationProvider
{
    /// <summary>
    /// Gets the key of the payment provider this configures, as <see cref="ICheckoutPaymentProvider.Key"/>.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the values a client needs to pay this checkout with the provider.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default);
}
