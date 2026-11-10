using CrestApps.OrchardCore.Locking;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Takes the locks time off runs under: one per worker (their balance) and one per allotment (its places), in a
/// fixed order so two changes can't deadlock. Work done under them is saved before they are let go.
/// </summary>
internal sealed class TimeOffLocks
{
    /// <summary>
    /// The lock area of a worker's time off balance and of an allotment's places.
    /// </summary>
    public const string TimeOffArea = "workforce.timeoff";

    /// <summary>
    /// The lock area of a worker's punches and timesheets.
    /// </summary>
    public const string TimeClockArea = "workforce.timeclock";

    /// <summary>
    /// The lock area of a pay period's payroll export.
    /// </summary>
    public const string PayrollExportArea = "workforce.payroll_export";

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
    /// <param name="keys">The lock keys.</param>
    /// <param name="area">The lock area recorded on the lock metrics, such as <see cref="TimeClockArea"/>.</param>
    public Task<ILocker> LockAsync(IEnumerable<string> keys, string area = TimeOffArea)
    {
        return _distributedLock.LockAllAsync(keys, _lockTimeout, _lockExpiration, area);
    }

    public static async Task ReleaseAsync(ILocker locker)
    {
        if (locker is not null)
        {
            await locker.DisposeAsync();
        }
    }
}
