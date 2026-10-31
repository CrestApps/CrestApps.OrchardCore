using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Plans a team's schedule: periods, draft scenarios and their shifts, rotations, copying, and publishing to the
/// team's Workforce calendar. A republish changes only the shifts that changed and records each change.
/// </summary>
public interface ISchedulePlanningService
{
    /// <summary>
    /// Creates a schedule period of some weeks for a team, with an empty primary scenario.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="startDate">The first day.</param>
    /// <param name="weeks">How many weeks it lasts.</param>
    /// <param name="name">A name.</param>
    /// <param name="actor">Who created it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The period, or why it was refused.</returns>
    Task<PlanningResult<SchedulePeriod>> CreatePeriodAsync(string teamId, DateOnly startDate, int weeks, string name, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a schedule period.
    /// </summary>
    /// <param name="id">The period id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The period, or <see langword="null"/>.</returns>
    Task<SchedulePeriod> FindPeriodAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of schedule periods, newest first.
    /// </summary>
    /// <param name="teamIds">The teams to list; <see langword="null"/> for every team.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<SchedulePeriod> Items)> ListPeriodsAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a period that was never published, with its scenarios and draft shifts.
    /// </summary>
    /// <param name="id">The period id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it was deleted.</returns>
    Task<bool> DeletePeriodAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the draft shifts of a scenario, by day and start.
    /// </summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The shifts.</returns>
    Task<IReadOnlyList<DraftShift>> ListDraftShiftsAsync(string scenarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new or changed draft shift. It must fall inside its period, belong to a worker on the team (or to
    /// nobody, as an open shift) and end after it starts.
    /// </summary>
    /// <param name="shift">The shift.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The saved shift, or why it was refused.</returns>
    Task<PlanningResult<DraftShift>> SaveDraftShiftAsync(DraftShift shift, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a draft shift.
    /// </summary>
    /// <param name="id">The draft shift id.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it existed.</returns>
    Task<bool> DeleteDraftShiftAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the shifts of the team's rotations to a scenario, for every worker who follows one, on the days of the
    /// period. Days that already have a shift for the worker are left alone.
    /// </summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many shifts were added.</returns>
    Task<int> ApplyPatternsAsync(string scenarioId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the primary scenario of another period into a scenario, moving each shift by the days between the two
    /// periods' starts. Shifts that fall outside the period are left out.
    /// </summary>
    /// <param name="scenarioId">The scenario to copy into.</param>
    /// <param name="sourcePeriodId">The period to copy from.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many shifts were copied.</returns>
    Task<int> CopyFromPeriodAsync(string scenarioId, string sourcePeriodId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a period's primary scenario to the team's calendar. Shifts with nobody stay off the calendar.
    /// </summary>
    /// <param name="periodId">The period.</param>
    /// <param name="actor">Who published.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was published.</returns>
    Task<PlanningPublishResult> PublishAsync(string periodId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the changes made to a period after it was first published, oldest first.
    /// </summary>
    /// <param name="periodId">The period.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The changes.</returns>
    Task<IReadOnlyList<ScheduleChange>> ListChangesAsync(string periodId, CancellationToken cancellationToken = default);
}
