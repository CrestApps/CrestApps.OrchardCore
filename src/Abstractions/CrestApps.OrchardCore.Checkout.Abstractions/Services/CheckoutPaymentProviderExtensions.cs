namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Finds and names the payment provider a stored provider key refers to, the same way on every screen.
/// </summary>
public static class CheckoutPaymentProviderExtensions
{
    /// <summary>
    /// Returns the provider whose key is <paramref name="providerKey"/>, ignoring case, or <see langword="null"/> when
    /// none is enabled.
    /// </summary>
    /// <param name="providers">The registered payment providers.</param>
    /// <param name="providerKey">The stored provider key.</param>
    public static ICheckoutPaymentProvider FindByKey(this IEnumerable<ICheckoutPaymentProvider> providers, string providerKey)
        => string.IsNullOrEmpty(providerKey) || providers is null
            ? null
            : providers.FirstOrDefault(provider => string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the name of a payment method, or its stored key when its provider is no longer enabled or has no name.
    /// </summary>
    /// <param name="providers">The registered payment providers, which name their own methods.</param>
    /// <param name="providerKey">The stored provider key.</param>
    /// <returns>The name, or <see langword="null"/> when no key is stored.</returns>
    public static string GetPaymentMethodName(this IEnumerable<ICheckoutPaymentProvider> providers, string providerKey)
    {
        if (string.IsNullOrEmpty(providerKey))
        {
            return null;
        }

        var provider = providers.FindByKey(providerKey);

        return string.IsNullOrEmpty(provider?.DisplayName) ? providerKey : provider.DisplayName;
    }
}
