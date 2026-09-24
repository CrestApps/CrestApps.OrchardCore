namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs a piece of routing work at the instant it falls due, on a scope of its own, instead of waiting for the
/// next background sweep to notice it.
/// </summary>
/// <remarks>
/// Orchard runs a tenant's background tasks one after another on a single loop, and several Contact Center tasks
/// hold that loop for most of a minute each, so a deadline that only a sweep enforces is enforced a minute or more
/// late: an offer that should have stopped ringing at thirty seconds kept the caller ringing for seventy. A deadline
/// held here fires within a second of when it is due. It is in-process only, so it is an accelerator, never the
/// record: the durable state still carries the deadline, and the sweeps remain the backstop for a restart, for work
/// scheduled on another node, and for anything this scheduler dropped. Every piece of work scheduled here must
/// therefore be idempotent and must re-read the durable state before acting.
/// </remarks>
public interface IContactCenterDeadlineScheduler
{
    /// <summary>
    /// Schedules <paramref name="work"/> to run at <paramref name="dueUtc"/> under <paramref name="key"/>,
    /// replacing whatever was scheduled under that key before.
    /// </summary>
    /// <param name="key">What the deadline belongs to; one deadline is held per key.</param>
    /// <param name="dueUtc">When the work falls due. A time already passed runs the work straight away.</param>
    /// <param name="work">The work, given the service provider of a fresh shell scope that commits when the work
    /// returns. It returns when to run again, or <see langword="null"/> when there is nothing more to do.</param>
    void Schedule(string key, DateTime dueUtc, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work);

    /// <summary>
    /// Drops the deadline held under <paramref name="key"/>, if there is one. Work already running is not stopped.
    /// </summary>
    /// <param name="key">The deadline's key.</param>
    void Cancel(string key);
}
