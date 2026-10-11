using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Persists <see cref="Subscription"/> agreements and answers the questions the rest of the suite asks of
/// them: who is subscribed, which agreement does this provider notification belong to, and what is due to
/// bill.
/// </summary>
public interface ISubscriptionStore : ICatalog<Subscription>
{
    /// <summary>
    /// Returns the subscription a provider's own identifier belongs to.
    /// </summary>
    /// <remarks>
    /// A provider notification names its own subscription and nothing else, so this lookup is the only
    /// correct way to correlate one. Matching on anything cached would drop events after a restart.
    /// </remarks>
    /// <param name="providerSubscriptionId">The provider's subscription identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscription created for one obligation of a checkout, which is what makes creating a
    /// subscription from a completed checkout idempotent.
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
    /// Returns the past-due subscriptions whose grace period has elapsed, so they can be expired instead of
    /// staying past due forever.
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

/// <summary>
/// The filter applied when listing subscriptions.
/// </summary>
public sealed class SubscriptionQuery
{
    /// <summary>
    /// Gets or sets the owner to restrict the results to.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the status to restrict the results to.
    /// </summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the payment provider to restrict the results to.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the reference type to restrict the results to.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the reference identifier to restrict the results to.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets a title search term.
    /// </summary>
    public string Search { get; set; }
}
