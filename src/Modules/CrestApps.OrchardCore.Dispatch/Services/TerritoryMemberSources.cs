using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// Lets a territory list the technicians who serve it.
/// </summary>
internal sealed class WorkerTerritoryMemberSource : ITerritoryMemberSource
{
    private readonly DispatchResources _resources;

    internal readonly IStringLocalizer S;

    public WorkerTerritoryMemberSource(DispatchResources resources, IStringLocalizer<WorkerTerritoryMemberSource> stringLocalizer)
    {
        _resources = resources;
        S = stringLocalizer;
    }

    public string Kind => DispatchConstants.ResourceKinds.Worker;

    public string DisplayName => S["Technicians"];

    public async Task<IReadOnlyList<(string Id, string Name)>> ListAsync(CancellationToken cancellationToken = default)
        => [.. (await _resources.ListWorkersAsync(cancellationToken)).OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(worker => (worker.ItemId, worker.DisplayName))];
}

/// <summary>
/// Lets a territory list the teams that serve it; each team's technicians serve it.
/// </summary>
internal sealed class TeamTerritoryMemberSource : ITerritoryMemberSource
{
    private readonly ITeamManager _teams;

    internal readonly IStringLocalizer S;

    public TeamTerritoryMemberSource(ITeamManager teams, IStringLocalizer<TeamTerritoryMemberSource> stringLocalizer)
    {
        _teams = teams;
        S = stringLocalizer;
    }

    public string Kind => "Team";

    public string DisplayName => S["Teams"];

    public async Task<IReadOnlyList<(string Id, string Name)>> ListAsync(CancellationToken cancellationToken = default)
        => [.. (await _teams.ListTreeAsync(cancellationToken)).Select(entry => (entry.Team.ItemId, new string(' ', entry.Depth * 2) + entry.Team.Name))];
}
