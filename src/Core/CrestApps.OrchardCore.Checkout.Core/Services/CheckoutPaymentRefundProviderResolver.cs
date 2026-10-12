using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutPaymentRefundProviderResolver"/> that resolves refund providers from
/// the ones registered in dependency injection. It guards against two providers registering the same key,
/// which would make refund routing non-deterministic, by preferring the first registration and logging
/// the conflict rather than silently choosing an arbitrary provider.
/// </summary>
public sealed class CheckoutPaymentRefundProviderResolver : ICheckoutPaymentRefundProviderResolver
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ICheckoutPaymentRefundProvider> _providersByKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentRefundProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered refund providers.</param>
    /// <param name="logger">The logger used to report duplicate provider keys.</param>
    public CheckoutPaymentRefundProviderResolver(
        IEnumerable<ICheckoutPaymentRefundProvider> providers,
        ILogger<CheckoutPaymentRefundProviderResolver> logger)
    {
        _logger = logger;
        _providersByKey = new Dictionary<string, ICheckoutPaymentRefundProvider>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            if (string.IsNullOrEmpty(provider.Key))
            {
                continue;
            }

            if (!_providersByKey.TryAdd(provider.Key, provider))
            {
                _logger.LogWarning(
                    "More than one checkout refund provider is registered with the key '{ProviderKey}'. The first registration is used; the duplicate '{ProviderType}' is ignored.",
                    provider.Key,
                    provider.GetType().FullName);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ICheckoutPaymentRefundProvider> GetProviders()
        => _providersByKey.Values;

    /// <inheritdoc/>
    public ICheckoutPaymentRefundProvider GetProvider(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return _providersByKey.GetValueOrDefault(key);
    }
}
