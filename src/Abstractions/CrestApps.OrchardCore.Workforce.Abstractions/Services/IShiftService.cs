using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Changes published shifts: gives a shift to another worker or swaps two, keeping the schedule's draft in step and
/// logging each change. Changes run under a lock per worker.
/// </summary>
public interface IShiftService
{
    /// <summary>
    /// Lists a worker's published shifts that overlap a range, on every team calendar.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shifts, soonest first.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListWorkerShiftsAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns why a worker can't be given a shift; empty when they can.
    /// </summary>
    /// <param name="context">The worker and the shift.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The problems.</returns>
    Task<IReadOnlyList<string>> CheckAsync(ShiftAssignmentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a published shift to another worker.
    /// </summary>
    /// <param name="eventId">The shift's calendar event.</param>
    /// <param name="workerId">The worker who takes it.</param>
    /// <param name="initiatedBy">Whether the employer or a worker asked for it.</param>
    /// <param name="reason">Why, for the change log.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The changed event, or why not.</returns>
    Task<PlanningResult<CalendarEvent>> ReassignAsync(string eventId, string workerId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swaps the workers of two published shifts.
    /// </summary>
    /// <param name="firstEventId">One shift's calendar event.</param>
    /// <param name="secondEventId">The other's.</param>
    /// <param name="initiatedBy">Whether the employer or a worker asked for it.</param>
    /// <param name="reason">Why, for the change log.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The two changed events, or why not.</returns>
    Task<PlanningResult<IReadOnlyList<CalendarEvent>>> SwapAsync(string firstEventId, string secondEventId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default);
}
