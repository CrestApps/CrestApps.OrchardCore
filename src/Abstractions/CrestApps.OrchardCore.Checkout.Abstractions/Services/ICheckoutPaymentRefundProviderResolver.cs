namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Resolves the registered <see cref="ICheckoutPaymentRefundProvider"/> instances so the checkout refund
/// service can drive a refund against the provider that owns the original payment. Implementations must
/// guard against two providers registering the same key, which would make refund routing non-deterministic.
/// </summary>
public interface ICheckoutPaymentRefundProviderResolver
{
    /// <summary>
    /// Gets every registered refund provider.
    /// </summary>
    IReadOnlyCollection<ICheckoutPaymentRefundProvider> GetProviders();

    /// <summary>
    /// Gets the refund provider with the given key, or <see langword="null"/> when none is registered.
    /// </summary>
    /// <param name="key">The provider key.</param>
    ICheckoutPaymentRefundProvider GetProvider(string key);
}
