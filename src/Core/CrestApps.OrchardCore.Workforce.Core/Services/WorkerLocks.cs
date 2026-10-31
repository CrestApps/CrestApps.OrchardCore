using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Takes the per-worker locks that changes to published shifts run under, in a fixed order so two changes over the
/// same workers can't deadlock.
/// </summary>
internal sealed class WorkerLocks
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedLock _distributedLock;

    public WorkerLocks(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// Locks the workers, or returns <see langword="null"/> when one is already locked.
    /// </summary>
    public async Task<List<ILocker>> LockAsync(IEnumerable<string> workerIds)
    {
        var lockers = new List<ILocker>();

        foreach (var id in workerIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync("WORKFORCE_WORKER_" + id, _lockTimeout, _lockExpiration);

            if (!locked)
            {
                await ReleaseAsync(lockers);

                return null;
            }

            lockers.Add(locker);
        }

        return lockers;
    }

    public static async Task ReleaseAsync(IEnumerable<ILocker> lockers)
    {
        foreach (var locker in lockers ?? [])
        {
            await locker.DisposeAsync();
        }
    }
}
