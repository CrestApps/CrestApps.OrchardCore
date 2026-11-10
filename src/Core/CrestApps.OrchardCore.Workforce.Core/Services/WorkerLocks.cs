using CrestApps.OrchardCore.Locking;
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
    public Task<ILocker> LockAsync(IEnumerable<string> workerIds)
    {
        return _distributedLock.LockAllAsync(workerIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => "WORKFORCE_WORKER_" + id), _lockTimeout, _lockExpiration, area: "workforce.worker");
    }

    public static async Task ReleaseAsync(ILocker locker)
    {
        if (locker is not null)
        {
            await locker.DisposeAsync();
        }
    }
}
