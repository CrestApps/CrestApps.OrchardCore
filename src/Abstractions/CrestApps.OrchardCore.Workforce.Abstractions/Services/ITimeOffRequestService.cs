using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Submits, decides and cancels time-off requests. A request is checked against blackout dates, the balance, the
/// worker's other requests and the allotments; one that fits an allotment that approves on its own is approved at
/// once, one that doesn't fit waits for a place or a manager, and canceling approved time off gives the place to the
/// first request waiting for it.
/// </summary>
public interface ITimeOffRequestService
{
    /// <summary>
    /// Works out what a request would count and what would happen to it, without saving anything.
    /// </summary>
    /// <param name="input">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The preview.</returns>
    Task<TimeOffPreview> PreviewAsync(TimeOffRequestInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a request.
    /// </summary>
    /// <param name="input">The request.</param>
    /// <param name="actor">Who submits it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why it was refused.</returns>
    Task<PlanningResult<TimeOffRequest>> SubmitAsync(TimeOffRequestInput input, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending or waitlisted request: puts it on the team calendar and takes it from the balance.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="actor">Who approves it.</param>
    /// <param name="note">A note to the worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why it can't be approved.</returns>
    Task<PlanningResult<TimeOffRequest>> ApproveAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns down a pending or waitlisted request.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="actor">Who declines it.</param>
    /// <param name="note">A note to the worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why it can't be declined.</returns>
    Task<PlanningResult<TimeOffRequest>> DeclineAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws an open request, or cancels approved time off: the event is canceled, the balance is given back and
    /// the first request waiting for the place gets it.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="actor">Who cancels it.</param>
    /// <param name="note">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why it can't be canceled.</returns>
    Task<PlanningResult<TimeOffRequest>> CancelAsync(string id, string actor, string note = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a request.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or <see langword="null"/>.</returns>
    Task<TimeOffRequest> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists requests, the earliest first.
    /// </summary>
    /// <param name="query">Which requests.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of requests and one page of them.</returns>
    Task<(int Count, IReadOnlyList<TimeOffRequest> Items)> PageAsync(TimeOffRequestQuery query, int skip, int take, CancellationToken cancellationToken = default);
}
