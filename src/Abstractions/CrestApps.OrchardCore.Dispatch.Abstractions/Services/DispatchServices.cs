using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// Makes, assigns, moves and moves along visits. Every change to who and when runs under a lock per worker and
/// checks again inside it that the workers are free.
/// </summary>
public interface IDispatchService
{
    /// <summary>
    /// Finds a visit.
    /// </summary>
    /// <param name="id">The visit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visit, or <see langword="null"/>.</returns>
    Task<ServiceVisit> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a work order's visits, oldest first.
    /// </summary>
    /// <param name="workOrderId">The work order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visits.</returns>
    Task<IReadOnlyList<ServiceVisit>> ListForWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the visits that overlap a time, of some workers or of everyone.
    /// </summary>
    /// <param name="fromUtc">The start, in UTC.</param>
    /// <param name="toUtc">The end, in UTC.</param>
    /// <param name="workerIds">The workers, or <see langword="null"/> for everyone.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visits, earliest first.</returns>
    Task<IReadOnlyList<ServiceVisit>> ListScheduledAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string> workerIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the visits waiting for a time and a worker, most urgent first.
    /// </summary>
    /// <param name="max">The most to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visits.</returns>
    Task<IReadOnlyList<ServiceVisit>> ListUnscheduledAsync(int max = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an unscheduled visit for a work order.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="actor">Who makes it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DispatchResult> CreateAsync(WorkOrder workOrder, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the open work orders that have no visit, such as those made before Dispatch was on.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The work orders' ids.</returns>
    Task<IReadOnlyList<string>> ListWorkOrdersWithoutVisitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an unscheduled visit for every open work order that has none.
    /// </summary>
    /// <param name="actor">Who makes them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were made.</returns>
    Task<int> CreateMissingAsync(string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a visit its workers and start, or moves it. The workers must be free, with travel and the buffer.
    /// </summary>
    /// <param name="visitId">The visit.</param>
    /// <param name="assignment">The workers and start.</param>
    /// <param name="actor">Who assigns it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DispatchResult> AssignAsync(string visitId, VisitAssignment assignment, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a scheduled visit off its workers, back to unscheduled.
    /// </summary>
    /// <param name="visitId">The visit.</param>
    /// <param name="actor">Who does it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DispatchResult> UnassignAsync(string visitId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a visit along: dispatched, en route, on site, paused, completed, incomplete or canceled.
    /// </summary>
    /// <param name="visitId">The visit.</param>
    /// <param name="status">The new status.</param>
    /// <param name="note">What is said about it; required for incomplete and canceled.</param>
    /// <param name="actor">Who does it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DispatchResult> ChangeStatusAsync(string visitId, VisitStatus status, string note, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a visit where it is, or lets it move again.
    /// </summary>
    /// <param name="visitId">The visit.</param>
    /// <param name="pinned">Whether it is pinned.</param>
    /// <param name="actor">Who does it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DispatchResult> PinAsync(string visitId, bool pinned, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds the workers who can take a visit, and ranks them.
/// </summary>
public interface IDispatchCandidateFinder
{
    /// <summary>
    /// Finds the workers who have the skills and valid certifications, are in the territory, are on shift and not on
    /// time off, and are free long enough with travel, between two times; then ranks them by the objective.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="fromUtc">The earliest start, in UTC.</param>
    /// <param name="toUtc">The latest end, in UTC.</param>
    /// <param name="objective">The weights, or <see langword="null"/> for the site's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidates and the workers ruled out.</returns>
    Task<DispatchCandidateResult> FindAsync(ServiceVisit visit, DateTime fromUtc, DateTime toUtc, DispatchObjective objective = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the workers who can take a visit of a work order given here rather than read from the store, such as
    /// one a booking would make, to see whether there is capacity for it.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="workOrder">Its work order, with its skills, place and window.</param>
    /// <param name="fromUtc">The earliest start, in UTC.</param>
    /// <param name="toUtc">The latest end, in UTC.</param>
    /// <param name="objective">The weights, or <see langword="null"/> for the site's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidates and the workers ruled out.</returns>
    Task<DispatchCandidateResult> FindAsync(ServiceVisit visit, WorkOrder workOrder, DateTime fromUtc, DateTime toUtc, DispatchObjective objective = null, CancellationToken cancellationToken = default)
        => FindAsync(visit, fromUtc, toUtc, objective, cancellationToken);

    /// <summary>
    /// Checks only what never changes in a day: that a worker may do the visit at all, with its skills,
    /// certifications and territory, leaving their time aside.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Why they can't, or <see langword="null"/> when they may.</returns>
    Task<string> CheckWorkerAsync(ServiceVisit visit, string workerId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hears about visit events after they happen, such as to start workflows or update the board.
/// </summary>
public interface IVisitObserver
{
    /// <summary>
    /// Called after a visit event.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="eventName">The event, one of <see cref="DispatchConstants.Events"/>.</param>
    /// <param name="detail">More about it, such as the previous status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task OnEventAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives the site's dispatch settings.
/// </summary>
public interface IDispatchSettingsProvider
{
    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    Task<DispatchSettings> GetAsync();

    /// <summary>
    /// Gets the time zone dispatch shows times in when a worker and their teams have none: the settings' or the site's.
    /// </summary>
    /// <returns>The time zone's id, or <see langword="null"/>.</returns>
    async Task<string> GetTimeZoneIdAsync() => (await GetAsync())?.TimeZoneId;
}
