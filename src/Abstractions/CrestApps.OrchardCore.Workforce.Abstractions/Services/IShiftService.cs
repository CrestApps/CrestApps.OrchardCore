using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Changes published shifts: gives a shift to another worker or swaps two, moves or removes one, or places and takes
/// back a segment another module owns, keeping the schedule's draft in step and logging each change. Changes run under
/// a lock per worker.
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

    /// <summary>
    /// Moves the start or end of a published shift, such as when a worker takes voluntary overtime or time off. The
    /// change is checked against the worker's rules and logged; segments outside the new times are dropped.
    /// </summary>
    /// <param name="eventId">The shift's calendar event.</param>
    /// <param name="startUtc">The new start.</param>
    /// <param name="endUtc">The new end.</param>
    /// <param name="initiatedBy">Whether the employer or the worker asked for it; a worker's change earns no
    /// predictability pay.</param>
    /// <param name="reason">Why, for the change log.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The changed event, or why not.</returns>
    Task<PlanningResult<CalendarEvent>> ChangeTimesAsync(string eventId, DateTime startUtc, DateTime endUtc, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a published shift off the schedule, such as when the work isn't needed after all. The shift's draft goes
    /// too, so the next publish doesn't bring it back, and the change is logged like any other.
    /// </summary>
    /// <param name="eventId">The shift's calendar event.</param>
    /// <param name="initiatedBy">Whether the employer or the worker asked for it; a worker's change earns no
    /// predictability pay.</param>
    /// <param name="reason">Why, for the change log.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The canceled event, or why not.</returns>
    Task<PlanningResult<CalendarEvent>> RemoveAsync(string eventId, ScheduleChangeInitiator initiatedBy, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a segment, such as training, on the worker's published shifts that overlap its time, clipped to each
    /// shift's times. A segment already placed by the same source and source id on those shifts is replaced, the
    /// schedule's draft is kept in step, and each changed shift is logged. Fails without changing anything when no
    /// published shift covers that time, because the worker isn't working then.
    /// </summary>
    /// <param name="placement">The segment and what places it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The changed events, or why not.</returns>
    Task<PlanningResult<IReadOnlyList<CalendarEvent>>> PlaceSegmentAsync(ShiftSegmentPlacement placement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the segments a source placed off a worker's published shifts in a range, and off their drafts, logging
    /// each changed shift.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="source">What placed the segments.</param>
    /// <param name="sourceId">The record of the source that placed them.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many segments were removed.</returns>
    Task<int> RemoveSegmentsAsync(string workerId, string source, string sourceId, DateTime fromUtc, DateTime toUtc, string actor, CancellationToken cancellationToken = default);
}
