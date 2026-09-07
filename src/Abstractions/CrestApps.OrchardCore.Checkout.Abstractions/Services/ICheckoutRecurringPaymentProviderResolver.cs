namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Resolves the registered <see cref="ICheckoutRecurringPaymentProvider"/> for a provider key, so the
/// checkout can establish a recurring agreement without knowing which providers exist.
/// </summary>
public interface ICheckoutRecurringPaymentProviderResolver
{
    /// <summary>
    /// Returns the recurring capability for the given provider key, or <see langword="null"/> when that
    /// provider cannot establish recurring agreements.
    /// </summary>
    /// <param name="providerKey">The provider key.</param>
    ICheckoutRecurringPaymentProvider GetProvider(string providerKey);
}
