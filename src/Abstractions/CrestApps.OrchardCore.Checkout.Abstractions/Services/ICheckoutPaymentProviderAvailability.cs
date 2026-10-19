namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Implemented by an <see cref="ICheckoutPaymentProvider"/> that is offered only on some checkouts: one the store
/// turned off, or cash on delivery for an order that isn't delivered. The payment step leaves it out, and the
/// checkout engine refuses to begin a payment with it, so a hand-made request can't choose it either.
/// </summary>
public interface ICheckoutPaymentProviderAvailability
{
    /// <summary>
    /// Returns whether the provider can be chosen to pay this checkout.
    /// </summary>
    /// <param name="session">The checkout being paid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default);
}
