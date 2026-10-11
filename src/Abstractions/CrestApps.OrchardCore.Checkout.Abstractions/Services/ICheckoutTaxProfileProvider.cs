using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Resolves the <see cref="CheckoutTaxProfile"/> for a checkout. Implementations extract the merchant
/// origin, customer destination, customer tax profile, and default classification from the flow so that
/// taxation is recalculated whenever tax-relevant information (such as the customer's address) changes.
/// </summary>
public interface ICheckoutTaxProfileProvider
{
    /// <summary>
    /// Gets the tax profile for the supplied flow.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CheckoutTaxProfile> GetProfileAsync(CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tax profile for a recurring billing cycle from the persisted session. The originating
    /// content is not available at billing time, so classification is sourced from the stored data while
    /// the destination is re-resolved so address changes take effect on future cycles.
    /// </summary>
    /// <param name="session">The checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CheckoutTaxProfile> GetProfileAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default);
}
