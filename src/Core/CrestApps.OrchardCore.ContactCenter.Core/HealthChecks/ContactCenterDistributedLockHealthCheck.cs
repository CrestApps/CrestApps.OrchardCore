using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Proves the resolved <see cref="IDistributedLock"/> can be acquired and released within a bounded time by
/// taking a dedicated probe lock. In a production topology this exercises the Redis-backed lock end to end; in
/// a development topology it exercises the process-local lock and is trivially satisfied. A failure here means
/// the lock backend cannot serialize the overlapping processes a rolling restart depends on.
/// </summary>
public sealed class ContactCenterDistributedLockHealthCheck : IHealthCheck
{
    internal const string ProbeLockKey = "CONTACTCENTER_HEALTHCHECK_DISTRIBUTED_LOCK";

    private static readonly TimeSpan _acquireTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(5);

    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDistributedLockHealthCheck"/> class.
    /// </summary>
    /// <param name="distributedLock">The resolved distributed lock to probe.</param>
    public ContactCenterDistributedLockHealthCheck(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (var locker, var acquired) = await _distributedLock.TryAcquireLockAsync(
                ProbeLockKey,
                _acquireTimeout,
                _lockExpiration);

            if (!acquired)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "The Contact Center distributed lock could not be acquired within the probe timeout.");
            }

            await using (locker)
            {
                return HealthCheckResult.Healthy("The Contact Center distributed lock is responsive.");
            }
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The Contact Center distributed lock backend is unreachable.",
                ex);
        }
    }
}
