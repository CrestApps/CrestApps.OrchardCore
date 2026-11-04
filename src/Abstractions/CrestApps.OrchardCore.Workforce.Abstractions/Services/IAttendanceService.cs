using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Attendance exceptions, their points, and workers' punch corrections.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Lists attendance exceptions, newest first.
    /// </summary>
    /// <param name="query">Which.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<AttendanceException> Items)> PageAsync(AttendanceQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Excuses an exception, so it earns no points.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="note">Why.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The exception, or the reasons it was refused.</returns>
    Task<PlanningResult<AttendanceException>> ExcuseAsync(string id, string note, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms an exception.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The exception, or the reasons it was refused.</returns>
    Task<PlanningResult<AttendanceException>> ConfirmAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a worker's attendance points on a day: the points of their exceptions that aren't excused, in the
    /// policy's window.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The points.</returns>
    Task<decimal> GetPointsAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks for a forgotten punch to be added.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="kind">The punch.</param>
    /// <param name="utc">When.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The correction, or the reasons it was refused.</returns>
    Task<PlanningResult<PunchCorrection>> RequestCorrectionAsync(WorkerProfile worker, TimePunchKind kind, DateTime utc, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a correction, adding the punch, or declines it.
    /// </summary>
    /// <param name="id">The correction.</param>
    /// <param name="approve">Whether to approve.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The correction, or the reasons it was refused.</returns>
    Task<PlanningResult<PunchCorrection>> DecideCorrectionAsync(string id, bool approve, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the pending corrections of some teams.
    /// </summary>
    /// <param name="teamIds">The teams; <see langword="null"/> for every team.</param>
    /// <param name="workerId">The worker, or <see langword="null"/> for every worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The corrections, oldest first.</returns>
    Task<IReadOnlyList<PunchCorrection>> ListPendingCorrectionsAsync(IReadOnlyCollection<string> teamIds, string workerId, CancellationToken cancellationToken = default);
}
