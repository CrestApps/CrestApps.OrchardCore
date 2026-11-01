using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out the team a worker's time off belongs to on a day, and the time zone their days are in.
/// </summary>
internal sealed class WorkerTimeZones : IWorkerTimeZoneService
{
    private readonly TeamTimeZoneResolver _teamTimeZones;

    public WorkerTimeZones(TeamTimeZoneResolver teamTimeZones)
    {
        _teamTimeZones = teamTimeZones;
    }

    /// <summary>
    /// Returns the worker's main team when they are on it that day, or else the first team they are on.
    /// </summary>
    public static string TeamOn(WorkerProfile worker, DateOnly date)
    {
        var memberships = worker.MembershipsOn(date).Select(membership => membership.TeamId).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        if (!string.IsNullOrEmpty(worker.PrimaryTeamId) && (memberships.Length == 0 || memberships.Contains(worker.PrimaryTeamId)))
        {
            return worker.PrimaryTeamId;
        }

        return memberships.FirstOrDefault() ?? worker.TeamMemberships.Select(membership => membership.TeamId).FirstOrDefault(id => !string.IsNullOrEmpty(id));
    }

    public async Task<string> GetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(worker.TimeZoneId))
        {
            return worker.TimeZoneId;
        }

        return await _teamTimeZones.GetAsync(teamId ?? TeamOn(worker, DateOnly.FromDateTime(worker.CreatedUtc)), cancellationToken);
    }

    public Task<string> GetTeamAsync(string teamId, CancellationToken cancellationToken = default)
        => _teamTimeZones.GetAsync(teamId, cancellationToken);
}
