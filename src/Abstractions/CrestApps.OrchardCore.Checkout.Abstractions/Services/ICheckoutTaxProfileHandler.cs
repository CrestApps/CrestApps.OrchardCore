namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Adds what a module knows to the tax profile of a checkout, such as the customer's exemption or the store's origin
/// address, before tax is worked out. The default profile provider runs every handler in turn.
/// </summary>
public interface ICheckoutTaxProfileHandler
{
    /// <summary>
    /// Called with the profile the provider built, at checkout and when a recurring charge is taxed.
    /// </summary>
    Task BuildingAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken = default);
}
