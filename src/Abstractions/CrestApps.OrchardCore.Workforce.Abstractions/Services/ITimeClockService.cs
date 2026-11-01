using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Records punches and corrects them.
/// </summary>
public interface ITimeClockService
{
    /// <summary>
    /// Returns the policy of the team a worker belongs to on a day, or its nearest parent's.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy, or <see langword="null"/>.</returns>
    Task<TimeClockPolicy> GetPolicyAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns where a worker stands on the clock.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The status.</returns>
    Task<TimeClockStatus> GetStatusAsync(WorkerProfile worker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a punch, once per idempotency key, when it follows the last one and the policy allows it.
    /// </summary>
    /// <param name="request">The punch.</param>
    /// <param name="actor">Who records it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The punch, or the reasons it was refused.</returns>
    Task<PlanningResult<TimePunch>> PunchAsync(TimePunchRequest request, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a punch's time or kind, recording a new punch that replaces it.
    /// </summary>
    /// <param name="punchId">The punch.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="utc">The UTC time.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new punch, or the reasons it was refused.</returns>
    Task<PlanningResult<TimePunch>> CorrectAsync(string punchId, TimePunchKind kind, DateTime utc, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a punch, recording a punch that removes it.
    /// </summary>
    /// <param name="punchId">The punch.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The removing punch, or the reasons it was refused.</returns>
    Task<PlanningResult<TimePunch>> RemoveAsync(string punchId, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a punch.
    /// </summary>
    /// <param name="punchId">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The punch, or <see langword="null"/>.</returns>
    Task<TimePunch> FindAsync(string punchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a worker's punches in a range, oldest first, corrections applied.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The punches that count.</returns>
    Task<IReadOnlyList<TimePunch>> ListAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the PIN a worker clocks in with on a kiosk, with their employee number as the badge.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="pin">The PIN, four to eight digits; empty to remove it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The worker, or the reasons it was refused.</returns>
    Task<PlanningResult<WorkerProfile>> SetPinAsync(WorkerProfile worker, string pin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the worker with a badge number and PIN, refusing for a while after five wrong PINs in a row.
    /// </summary>
    /// <param name="badgeNumber">The badge number.</param>
    /// <param name="pin">The PIN.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The worker, or the reason they weren't found.</returns>
    Task<PlanningResult<WorkerProfile>> IdentifyAsync(string badgeNumber, string pin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the kiosk in use that an address's secret opens.
    /// </summary>
    /// <param name="token">The secret.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The kiosk, or <see langword="null"/>.</returns>
    Task<TimeClockKiosk> FindKioskAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a kiosk a new secret, which stops the old address working.
    /// </summary>
    /// <param name="kiosk">The kiosk.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new secret, shown once.</returns>
    Task<string> RenewKioskTokenAsync(TimeClockKiosk kiosk, CancellationToken cancellationToken = default);
}
