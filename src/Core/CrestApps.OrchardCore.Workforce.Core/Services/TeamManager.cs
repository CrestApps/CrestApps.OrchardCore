using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITeamManager"/>.
/// </summary>
internal sealed class TeamManager : CatalogManager<WorkforceTeam>, ITeamManager
{
    private readonly TeamStore _store;

    public TeamManager(
        TeamStore store,
        IEnumerable<ICatalogEntryHandler<WorkforceTeam>> handlers,
        ILogger<CatalogManager<WorkforceTeam>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<(WorkforceTeam Team, int Depth)>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        var teams = await _store.ListAsync(cancellationToken);

        foreach (var team in teams)
        {
            await LoadAsync(team, cancellationToken);
        }

        return TeamTree.Order(teams);
    }

    public async Task<IReadOnlySet<string>> GetSubtreeIdsAsync(string teamId, CancellationToken cancellationToken = default)
    {
        return await TeamTree.GetSubtreeIdsAsync(await _store.ListAsync(cancellationToken), teamId);
    }

    public async Task<IReadOnlyList<WorkforceTeam>> GetAncestorsAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var teams = (await _store.ListAsync(cancellationToken)).ToDictionary(team => team.ItemId, StringComparer.Ordinal);
        var ancestors = new List<WorkforceTeam>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { teamId };

        for (var current = teams.GetValueOrDefault(teamId ?? string.Empty)?.ParentId;
            !string.IsNullOrEmpty(current) && seen.Add(current) && teams.TryGetValue(current, out var parent);
            current = parent.ParentId)
        {
            ancestors.Add(parent);
        }

        return ancestors;
    }
}
