using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Creates the site recorded by a <see cref="TenantProvisioningJob"/>.
/// </summary>
/// <remarks>
/// It is a service rather than only a background task so the work can also be triggered on demand, for
/// example by an operator retrying a job that failed for a reason they have since fixed.
/// </remarks>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Attempts to create the site for one job and returns the state the job is left in.
    /// </summary>
    /// <remarks>
    /// The attempt is claimed under a distributed lock, so calling this concurrently for the same job builds
    /// the site once. It never throws for a provisioning failure: the failure is recorded on the job with a
    /// back-off, because a customer who has paid needs the attempt tracked rather than lost.
    /// </remarks>
    /// <param name="jobId">The provisioning job identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TenantProvisioningStatus> ProvisionAsync(string jobId, CancellationToken cancellationToken = default);
}
