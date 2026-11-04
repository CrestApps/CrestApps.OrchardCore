using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Keeps workers' timesheets: works them out, and takes them through submit, approve, reject, reopen and lock.
/// </summary>
public interface ITimesheetService
{
    /// <summary>
    /// Returns a worker's timesheet for the pay period a day falls in, created and worked out when needed.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons there is none, such as no policy.</returns>
    Task<PlanningResult<Timesheet>> GetForDateAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a timesheet.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or <see langword="null"/>.</returns>
    Task<Timesheet> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works a timesheet out again from punches, time off and premiums, unless it is submitted, approved or locked.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet and its calculation.</returns>
    Task<(Timesheet Timesheet, TimesheetCalculation Calculation)> CalculateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists timesheets.
    /// </summary>
    /// <param name="query">Which.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<Timesheet> Items)> PageAsync(TimesheetQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a timesheet with the worker's attestation.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="attestation">What the worker attests to.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> SubmitAsync(string id, string attestation, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a timesheet.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a timesheet back to the worker.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> RejectAsync(string id, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an approved timesheet again; a locked one stays locked.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> ReopenAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the approved timesheets of a pay period that has ended in their worker's time zone, ready for payroll.
    /// </summary>
    /// <param name="payPeriodCalendarId">The pay periods.</param>
    /// <param name="periodStart">The period's first day.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheets locked.</returns>
    Task<IReadOnlyList<Timesheet>> LockPeriodAsync(string payPeriodCalendarId, DateOnly periodStart, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a line by hand, such as holiday or call-back hours.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="line">The line.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> AddLineAsync(string id, TimesheetLine line, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a line added by hand.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="lineId">The line.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timesheet, or the reasons it was refused.</returns>
    Task<PlanningResult<Timesheet>> RemoveLineAsync(string id, string lineId, string actor, CancellationToken cancellationToken = default);
}
