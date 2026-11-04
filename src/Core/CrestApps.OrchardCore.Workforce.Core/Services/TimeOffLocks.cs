using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Takes the locks time off runs under: one per worker (their balance) and one per allotment (its places), in a
/// fixed order so two changes can't deadlock. Work done under them is saved before they are let go.
/// </summary>
internal sealed class TimeOffLocks
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedLock _distributedLock;

    public TimeOffLocks(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    public static string Worker(string workerId)
    {
        return "WORKFORCE_TIMEOFF_" + workerId;
    }

    public static string Allotment(string allotmentId)
    {
        return "WORKFORCE_ALLOTMENT_" + allotmentId;
    }

    /// <summary>
    /// The lock a worker's punches and timesheets change under.
    /// </summary>
    public static string TimeClock(string workerId)
    {
        return "WORKFORCE_TIMECLOCK_" + workerId;
    }

    /// <summary>
    /// The lock a pay period's payroll export runs under.
    /// </summary>
    public static string PayrollExport(string calendarId, DateOnly periodStart)
    {
        return $"WORKFORCE_PAYROLL_{calendarId}_{periodStart:yyyyMMdd}";
    }

    /// <summary>
    /// Takes the locks, or returns <see langword="null"/> when one is held too long by someone else.
    /// </summary>
    public async Task<List<ILocker>> LockAsync(IEnumerable<string> keys)
    {
        var lockers = new List<ILocker>();

        foreach (var key in keys.Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, _lockTimeout, _lockExpiration);

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
