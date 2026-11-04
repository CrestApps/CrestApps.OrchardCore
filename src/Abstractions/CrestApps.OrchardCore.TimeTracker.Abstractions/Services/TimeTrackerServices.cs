using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;

namespace CrestApps.OrchardCore.TimeTracker.Services;

/// <summary>
/// Tracks people's time: clocking in, switching stage, clocking out, and the edits managers make. A person's entries
/// never overlap, and every change runs under a lock per person.
/// </summary>
public interface ITimeTrackerService
{
    /// <summary>
    /// Gets a person's clock now.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The clock.</returns>
    Task<TimeTrackerStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a person in a stage from now: clocks them in, or ends the stage they are in and starts this one.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="userName">The person's user name, for lists.</param>
    /// <param name="stageId">The stage.</param>
    /// <param name="source">Where it came from, one of <see cref="TimeTrackerConstants.Sources"/>.</param>
    /// <param name="actor">Who did it.</param>
    /// <param name="sourceRef">What in the source it stands for, if anything.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the new entry.</returns>
    Task<TimeTrackerResult> SwitchAsync(string userId, string userName, string stageId, string source, string actor, string sourceRef = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clocks a person out: ends the stage they are in now.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="actor">Who did it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the ended entry.</returns>
    Task<TimeTrackerResult> ClockOutAsync(string userId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an entry that has already ended, such as one a manager adds or approved time off.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="actor">Who adds it.</param>
    /// <param name="reason">Why, kept with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<TimeTrackerResult> AddAsync(TimeEntry entry, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes an entry's stage, times or note; the change and the reason are kept with it.
    /// </summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="stageId">The stage.</param>
    /// <param name="startUtc">The start, in UTC.</param>
    /// <param name="endUtc">The end, in UTC, or <see langword="null"/> to leave an open entry open.</param>
    /// <param name="note">The note.</param>
    /// <param name="actor">Who changes it.</param>
    /// <param name="reason">Why; required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<TimeTrackerResult> UpdateAsync(string entryId, string stageId, DateTime startUtc, DateTime? endUtc, string note, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entry.
    /// </summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="actor">Who removes it.</param>
    /// <param name="reason">Why; required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the removed entry.</returns>
    Task<TimeTrackerResult> DeleteAsync(string entryId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an entry.
    /// </summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<TimeEntry> FindAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists entries that overlap a range, earliest first.
    /// </summary>
    /// <param name="query">What to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<TimeEntry>> ListAsync(TimeEntryQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Totals people's time in a range by category and stage, for each person or for each person and day. An open
    /// entry counts up to now.
    /// </summary>
    /// <param name="query">The people and range.</param>
    /// <param name="timeZoneId">The time zone days are counted in, when by day.</param>
    /// <param name="byDay">Whether to total each day apart.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A row for each person, or for each person and day.</returns>
    Task<IReadOnlyList<TimeTotalsRow>> GetTotalsAsync(TimeEntryQuery query, string timeZoneId, bool byDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the entries another clock keeps for a person over a range with the ones it builds now. Entries that
    /// didn't change keep their ids; time kept here (a manager's entry, say) stays and the clock's entries are cut
    /// around it.
    /// </summary>
    /// <param name="sync">The person, the clock, the range and how to build the entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<TimeTrackerResult> SyncAsync(TimeEntrySync sync, CancellationToken cancellationToken = default);
}

/// <summary>
/// Says when another clock keeps a person's time, such as a time clock that records punches. Their time is then
/// switched and corrected there, not here.
/// </summary>
public interface IExternalTimeClock
{
    /// <summary>
    /// Finds the clock that keeps a person's time.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The clock, or <see langword="null"/> when this clock does not keep their time.</returns>
    Task<ExternalTimeClockInfo> FindAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a clock by its id.
    /// </summary>
    /// <param name="id">The id kept in <see cref="TimeEntry.ManagedBy"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The clock, or <see langword="null"/> when it isn't this one.</returns>
    Task<ExternalTimeClockInfo> FindByIdAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hears about every change to a time entry after it is made, such as to record it in the Audit Trail.
/// </summary>
public interface ITimeEntryObserver
{
    /// <summary>
    /// Called after a change.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="entry">The entry as it is now (as it was, when deleted).</param>
    /// <param name="before">What it was before an edit, as text, or <see langword="null"/>.</param>
    /// <param name="actor">Who did it.</param>
    /// <param name="reason">Why, if given.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task OnChangedAsync(TimeEntryChangeKind kind, TimeEntry entry, string before, string actor, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages time stages.
/// </summary>
public interface ITimeStageManager : ICatalogManager<TimeStage>
{
}

/// <summary>
/// Manages time categories.
/// </summary>
public interface ITimeCategoryManager : ICatalogManager<TimeCategory>
{
}
