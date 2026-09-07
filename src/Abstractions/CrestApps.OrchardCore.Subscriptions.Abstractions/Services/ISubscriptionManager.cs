using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The manager for <see cref="Subscription"/> agreements. It extends the generic catalog manager with the
/// lookups the suite needs, running loaded entries through the registered catalog handlers.
/// </summary>
public interface ISubscriptionManager : ICatalogManager<Subscription>
{
    /// <summary>
    /// Returns the subscription a provider's own identifier belongs to.
    /// </summary>
    /// <param name="providerSubscriptionId">The provider's subscription identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscription created for one obligation of a checkout.
    /// </summary>
    /// <param name="checkoutSessionId">The checkout session identifier.</param>
    /// <param name="obligationId">The obligation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscriptions created from one checkout.
    /// </summary>
    /// <param name="checkoutSessionId">The checkout session identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Subscription>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every subscription owned by a user.
    /// </summary>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Subscription>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscriptions whose next billing time has passed and which are still billing.
    /// </summary>
    /// <param name="asOfUtc">The UTC time to evaluate against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Subscription>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the past-due subscriptions whose grace period has elapsed.
    /// </summary>
    /// <param name="asOfUtc">The UTC time to evaluate against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Subscription>> GetLapsedAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of subscriptions for the administration report.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<Subscription>> PageAsync(int page, int pageSize, SubscriptionQuery query, CancellationToken cancellationToken = default);
}
