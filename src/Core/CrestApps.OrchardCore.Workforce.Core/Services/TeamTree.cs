using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Walks the team hierarchy held in memory. A site has tens or hundreds of teams, so the whole tree is read at once.
/// </summary>
internal static class TeamTree
{
    public static Task<IReadOnlySet<string>> GetSubtreeIdsAsync(IEnumerable<WorkforceTeam> teams, string teamId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(teamId))
        {
            return Task.FromResult<IReadOnlySet<string>>(result);
        }

        var children = teams
            .Where(team => !string.IsNullOrEmpty(team.ParentId))
            .ToLookup(team => team.ParentId, StringComparer.Ordinal);
        var pending = new Stack<string>();

        pending.Push(teamId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!result.Add(current))
            {
                continue;
            }

            foreach (var child in children[current])
            {
                pending.Push(child.ItemId);
            }
        }

        return Task.FromResult<IReadOnlySet<string>>(result);
    }

    public static IReadOnlyList<(WorkforceTeam Team, int Depth)> Order(IEnumerable<WorkforceTeam> teams)
    {
        var all = teams.ToList();
        var ids = all.Select(team => team.ItemId).ToHashSet(StringComparer.Ordinal);
        var children = all
            .Where(team => !string.IsNullOrEmpty(team.ParentId) && ids.Contains(team.ParentId))
            .ToLookup(team => team.ParentId, StringComparer.Ordinal);
        var result = new List<(WorkforceTeam, int)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(WorkforceTeam team, int depth)
        {
            if (!visited.Add(team.ItemId))
            {
                return;
            }

            result.Add((team, depth));

            foreach (var child in children[team.ItemId].OrderBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Visit(child, depth + 1);
            }
        }

        // A team whose parent is missing is shown at the top, so nothing disappears from the tree.
        foreach (var root in all.Where(team => string.IsNullOrEmpty(team.ParentId) || !ids.Contains(team.ParentId)).OrderBy(team => team.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Visit(root, 0);
        }

        return result;
    }
}
