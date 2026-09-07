using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Persists the tenants that have been paid for and must be created.
/// </summary>
public interface ITenantProvisioningJobStore : ICatalog<TenantProvisioningJob>
{
    /// <summary>
    /// Returns the job created for one checkout, which is what makes recording the intent idempotent when a
    /// checkout completes more than once.
    /// </summary>
    /// <param name="checkoutSessionId">The checkout session identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TenantProvisioningJob> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the job that created a tenant, so the tenant can be suspended when the subscription behind it
    /// lapses.
    /// </summary>
    /// <param name="tenantName">The Orchard Core tenant name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TenantProvisioningJob> GetByTenantNameAsync(string tenantName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the jobs that are ready to be attempted.
    /// </summary>
    /// <param name="asOfUtc">The UTC time to evaluate the back-off against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<TenantProvisioningJob>> GetDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every job bought by a user, so the buyer can watch their site being created.
    /// </summary>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<TenantProvisioningJob>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
}
