using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Offers shifts with nobody to the team's eligible workers, takes claims, and gives the shift to a worker by the
/// shift's claim policy. A filled shift goes on the team calendar and, for a scheduled one, into the schedule.
/// </summary>
public interface IOpenShiftService
{
    /// <summary>
    /// Posts an open shift on its own, outside any schedule.
    /// </summary>
    /// <param name="shift">The shift.</param>
    /// <param name="actor">Who posted it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The posted shift, or why it was refused.</returns>
    Task<PlanningResult<OpenShift>> PostAsync(OpenShift shift, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an open shift.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shift, or <see langword="null"/>.</returns>
    Task<OpenShift> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of open shifts, soonest first.
    /// </summary>
    /// <param name="query">Which shifts.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<OpenShift> Items)> PageAsync(OpenShiftQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns why a worker can't take an open shift; empty when they can.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The problems.</returns>
    Task<IReadOnlyList<string>> CheckAsync(string id, string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the team's workers who can take an open shift.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The workers.</returns>
    Task<IReadOnlyList<WorkerProfile>> ListEligibleWorkersAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a worker's claim. Under <see cref="OpenShiftClaimPolicy.FirstCome"/> the worker gets the shift at once.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="actor">Who claimed, the worker or someone on their behalf.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shift, or why the claim was refused.</returns>
    Task<PlanningResult<OpenShift>> ClaimAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back a worker's pending claim.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="actor">Who withdrew it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shift, or why not.</returns>
    Task<PlanningResult<OpenShift>> WithdrawClaimAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the shift to a worker, whether or not they claimed it. Their claim, if any, is approved and the others
    /// declined.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="actor">Who decided.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filled shift, or why not.</returns>
    Task<PlanningResult<OpenShift>> AssignAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns down a worker's pending claim.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="note">Why, shown to the worker.</param>
    /// <param name="actor">Who decided.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shift, or why not.</returns>
    Task<PlanningResult<OpenShift>> DeclineClaimAsync(string id, string workerId, string note, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops offering an open shift that isn't filled.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="actor">Who canceled it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it was canceled.</returns>
    Task<bool> CancelAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Awards the open shifts given by seniority whose claim deadline has passed, each to the most senior eligible
    /// claimant.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were awarded.</returns>
    Task<int> AwardDueAsync(CancellationToken cancellationToken = default);
}
