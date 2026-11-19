using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// The storage the tracker and the monitor keep service level timers in.
/// </summary>
public interface IServiceLevelTimerStore
{
    /// <summary>
    /// Adds a timer.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    ValueTask CreateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a timer.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    ValueTask UpdateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a timer only when nobody changed it since it was read: the next <see cref="FlushAsync"/> fails with a
    /// concurrency exception otherwise. The monitor saves this way, so it never writes over a timer its owner just
    /// completed.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task UpdateIfUnchangedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a timer in a transaction of its own, read right before the write, only while it is still due: a timer its
    /// owner completed, paused or retargeted meanwhile is left alone. The monitor marks warnings and breaches this way,
    /// so an owner's change always wins and an owner never fails on the monitor.
    /// </summary>
    /// <param name="timerId">The timer's id.</param>
    /// <param name="stillDue">Whether the timer, as read now, still needs the mark.</param>
    /// <param name="mark">Sets the mark.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timer as saved, or <see langword="null"/> when it no longer needed the mark or changed meanwhile.</returns>
    Task<ServiceLevelTimer> MarkIfStillDueAsync(string timerId, Func<ServiceLevelTimer, bool> stillDue, Action<ServiceLevelTimer> mark, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the pending changes to the database now.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the timers of a record, newest first.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="activeOnly">Whether only running and paused timers are returned.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> ListBySubjectAsync(RecordRef subject, bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the running timers of some policies.
    /// </summary>
    /// <param name="policyIds">The policies.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> ListRunningByPoliciesAsync(IEnumerable<string> policyIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a policy has a timer that is running or paused.
    /// </summary>
    /// <param name="policyId">The policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    Task<bool> AnyActiveAsync(string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists running timers that passed their target and aren't marked breached yet, the most overdue first.
    /// </summary>
    /// <param name="nowUtc">The current instant, in UTC.</param>
    /// <param name="take">The most timers to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyBreachedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists running timers that reached their warning point, aren't breached and weren't warned about yet.
    /// </summary>
    /// <param name="nowUtc">The current instant, in UTC.</param>
    /// <param name="take">The most timers to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyWarnedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
}
