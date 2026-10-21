namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Implemented by an <see cref="ICheckoutPaymentProvider"/> that says in its own words what choosing it means, under
/// its name on the payment step, such as "Pay when your order is delivered."
/// </summary>
public interface ICheckoutPaymentProviderDescription
{
    /// <summary>
    /// Gets the line shown under the provider's name at checkout.
    /// </summary>
    string Description { get; }
}
