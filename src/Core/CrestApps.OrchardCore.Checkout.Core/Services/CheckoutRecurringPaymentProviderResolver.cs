using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutRecurringPaymentProviderResolver"/>. It resolves recurring capabilities
/// from the providers registered in dependency injection, preferring the first registration when two share a
/// key and logging the conflict rather than routing a recurring agreement to an arbitrary provider.
/// </summary>
public sealed class CheckoutRecurringPaymentProviderResolver : ICheckoutRecurringPaymentProviderResolver
{
    private readonly Dictionary<string, ICheckoutRecurringPaymentProvider> _providersByKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutRecurringPaymentProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered recurring payment providers.</param>
    /// <param name="logger">The logger used to report duplicate provider keys.</param>
    public CheckoutRecurringPaymentProviderResolver(
        IEnumerable<ICheckoutRecurringPaymentProvider> providers,
        ILogger<CheckoutRecurringPaymentProviderResolver> logger)
    {
        _providersByKey = new Dictionary<string, ICheckoutRecurringPaymentProvider>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            if (string.IsNullOrEmpty(provider.Key))
            {
                continue;
            }

            if (!_providersByKey.TryAdd(provider.Key, provider))
            {
                logger.LogWarning(
                    "More than one recurring payment provider is registered with the key '{ProviderKey}'. The first registration is used; the duplicate '{ProviderType}' is ignored.",
                    provider.Key,
                    provider.GetType().FullName);
            }
        }
    }

    /// <inheritdoc/>
    public ICheckoutRecurringPaymentProvider GetProvider(string providerKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerKey);

        return _providersByKey.GetValueOrDefault(providerKey);
    }
}
