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
}
