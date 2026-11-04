using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Teams.Core.Services;

/// <summary>
/// The default <see cref="ITeamManager"/>.
/// </summary>
internal sealed class TeamManager : CatalogManager<Team>, ITeamManager
{
    private readonly TeamStore _store;
    private readonly IEnumerable<ITeamDeletionGuard> _deletionGuards;

    internal readonly IStringLocalizer S;

    public TeamManager(
        TeamStore store,
        IEnumerable<ICatalogEntryHandler<Team>> handlers,
        IEnumerable<ITeamDeletionGuard> deletionGuards,
        ILogger<CatalogManager<Team>> logger,
        IStringLocalizer<TeamManager> stringLocalizer)
        : base(store, handlers, logger)
    {
        _store = store;
        _deletionGuards = deletionGuards;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<(Team Team, int Depth)>> ListTreeAsync(CancellationToken cancellationToken = default)
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
        return TeamTree.GetSubtreeIds(await _store.ListAsync(cancellationToken), teamId);
    }

    public async Task<IReadOnlyList<Team>> GetAncestorsAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var teams = (await _store.ListAsync(cancellationToken)).ToDictionary(team => team.ItemId, StringComparer.Ordinal);
        var ancestors = new List<Team>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { teamId };

        for (var current = teams.GetValueOrDefault(teamId ?? string.Empty)?.ParentId;
            !string.IsNullOrEmpty(current) && seen.Add(current) && teams.TryGetValue(current, out var parent);
            current = parent.ParentId)
        {
            ancestors.Add(parent);
        }

        return ancestors;
    }

    public async Task<IReadOnlyList<string>> GetDeletionBlockersAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var blockers = new List<string>();

        if ((await _store.ListAsync(cancellationToken)).Any(child => child.ParentId == team.ItemId))
        {
            blockers.Add(S["The team still has sub-teams. Move or delete them first."]);
        }

        foreach (var guard in _deletionGuards)
        {
            var blocker = await guard.GetBlockerAsync(team, cancellationToken);

            if (!string.IsNullOrEmpty(blocker))
            {
                blockers.Add(blocker);
            }
        }

        return blockers;
    }
}
