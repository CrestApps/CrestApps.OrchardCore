using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// One technician's row on the dispatch board.
/// </summary>
/// <param name="Worker">The technician.</param>
/// <param name="Team">The technician's team on the day, or <see langword="null"/>.</param>
/// <param name="Availability">The technician's open time, time off and shift on the day.</param>
internal sealed record DispatchBoardRow(StaffMember Worker, Team Team, WorkerAvailability Availability);

/// <summary>
/// What the dispatch board shows for a day.
/// </summary>
internal sealed class DispatchBoardDay
{
    /// <summary>
    /// Gets the start of the day, in UTC.
    /// </summary>
    public DateTime FromUtc { get; init; }

    /// <summary>
    /// Gets the end of the day, in UTC.
    /// </summary>
    public DateTime ToUtc { get; init; }

    /// <summary>
    /// Gets the technicians shown, by name.
    /// </summary>
    public IReadOnlyList<DispatchBoardRow> Rows { get; init; } = [];

    /// <summary>
    /// Gets the visits scheduled on the day for those technicians (for everyone without a team filter).
    /// </summary>
    public IReadOnlyList<ServiceVisit> Scheduled { get; init; } = [];

    /// <summary>
    /// Gets the visits waiting for a time, up to 200.
    /// </summary>
    public IReadOnlyList<ServiceVisit> Unscheduled { get; init; } = [];
}

/// <summary>
/// Loads the dispatch board's day: the technicians (of a team and its sub-teams, or with a capability, or the dispatch
/// settings' technicians) with their availability, the day's scheduled visits and the unscheduled visits.
/// </summary>
internal static class DispatchBoardDayQuery
{
    /// <summary>
    /// The most unscheduled visits the board shows.
    /// </summary>
    public const int MaxUnscheduled = 200;

    /// <summary>
    /// Loads a day of the board.
    /// </summary>
    /// <param name="resources">The dispatch resources.</param>
    /// <param name="dispatch">The dispatch service.</param>
    /// <param name="teams">The team manager.</param>
    /// <param name="date">The day, in <paramref name="zone"/>.</param>
    /// <param name="zone">The dispatch time zone.</param>
    /// <param name="teamId">The team, or <see langword="null"/> for every team.</param>
    /// <param name="capability">The capability, or <see langword="null"/> for the dispatch settings' technician capabilities.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The day.</returns>
    public static async Task<DispatchBoardDay> LoadAsync(DispatchResources resources, IDispatchService dispatch, ITeamManager teams, DateOnly date, string zone, string teamId, string capability, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(teams);

        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var teamIds = string.IsNullOrEmpty(teamId) ? null : await teams.GetSubtreeIdsAsync(teamId, cancellationToken);
        var workers = (await resources.ListWorkersAsync(Capabilities(capability), cancellationToken))
            .Where(worker => teamIds is null || worker.MembershipsOn(date).Any(membership => teamIds.Contains(membership.TeamId)))
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var rows = new List<DispatchBoardRow>();

        foreach (var worker in workers)
        {
            var availability = await resources.GetAvailabilityAsync(worker, fromUtc, toUtc, null, cancellationToken);
            var team = await resources.FindTeamAsync(worker.PrimaryTeamId ?? worker.MembershipsOn(date).Select(membership => membership.TeamId).FirstOrDefault(), cancellationToken);

            rows.Add(new DispatchBoardRow(worker, team, availability));
        }

        var scheduled = await dispatch.ListScheduledAsync(fromUtc, toUtc, teamIds is null ? null : workers.Select(worker => worker.ItemId).ToArray(), cancellationToken);
        var unscheduled = await dispatch.ListUnscheduledAsync(MaxUnscheduled, cancellationToken);

        return new DispatchBoardDay
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Rows = rows,
            Scheduled = scheduled,
            Unscheduled = unscheduled,
        };
    }

    // A capability picked on the board, or none for the dispatch settings' ones.
    private static string[] Capabilities(string capability)
        => string.IsNullOrWhiteSpace(capability) ? null : [capability.Trim()];
}
