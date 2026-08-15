namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Resolves the registered <see cref="ICheckoutPaymentProvider"/> instances so the checkout framework can
/// drive begin/verify/cancel against the provider that owns a given attempt.
/// </summary>
public interface ICheckoutPaymentProviderResolver
{
    /// <summary>
    /// Gets every registered payment provider.
    /// </summary>
    IReadOnlyCollection<ICheckoutPaymentProvider> GetProviders();

    /// <summary>
    /// Gets the provider with the given key, or <c>null</c> when none is registered.
    /// </summary>
    /// <param name="key">The provider key.</param>
    ICheckoutPaymentProvider GetProvider(string key);
}
