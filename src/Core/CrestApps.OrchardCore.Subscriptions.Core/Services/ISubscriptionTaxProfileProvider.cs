using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Resolves the <see cref="SubscriptionTaxProfile"/> for a subscription flow. Implementations extract
/// the merchant origin, customer destination, customer tax profile, and default classification from the
/// flow so that taxation is recalculated whenever tax-relevant information (such as the customer's
/// address) changes. Register a custom implementation to source addresses from your own checkout data.
/// </summary>
public interface ISubscriptionTaxProfileProvider
{
    /// <summary>
    /// Gets the tax profile for the supplied flow.
    /// </summary>
    /// <param name="flow">The subscription flow.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SubscriptionTaxProfile> GetProfileAsync(SubscriptionFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tax profile for a recurring billing cycle from the persisted session. The content item
    /// is not available at billing time, so classification is sourced from the stored subscription data
    /// while the destination is re-resolved so that address changes take effect on future cycles.
    /// </summary>
    /// <param name="session">The subscription session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SubscriptionTaxProfile> GetProfileAsync(ISubscriptionFlowSession session, CancellationToken cancellationToken = default);
}
