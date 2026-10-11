using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutPaymentProviderResolver"/> that resolves providers from the ones
/// registered in dependency injection.
/// </summary>
public sealed class CheckoutPaymentProviderResolver : ICheckoutPaymentProviderResolver
{
    private readonly IEnumerable<ICheckoutPaymentProvider> _providers;

    public CheckoutPaymentProviderResolver(IEnumerable<ICheckoutPaymentProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ICheckoutPaymentProvider> GetProviders()
        => _providers.ToArray();

    /// <inheritdoc/>
    public ICheckoutPaymentProvider GetProvider(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return _providers.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
    }
}
