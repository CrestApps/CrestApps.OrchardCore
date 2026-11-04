using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Core.Services;

/// <summary>
/// Walks the team hierarchy held in memory. A site has tens or hundreds of teams, so the whole tree is read at once.
/// </summary>
public static class TeamTree
{
    /// <summary>
    /// Gets the ids of a team and every team under it.
    /// </summary>
    /// <param name="teams">Every team.</param>
    /// <param name="teamId">The team.</param>
    /// <returns>The team id and its descendants' ids; empty when <paramref name="teamId"/> is empty.</returns>
    public static IReadOnlySet<string> GetSubtreeIds(IEnumerable<Team> teams, string teamId)
    {
        ArgumentNullException.ThrowIfNull(teams);

        var result = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(teamId))
        {
            return result;
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

        return result;
    }

    /// <summary>
    /// Orders teams for a tree: each team followed by its sub-teams by name, with its depth. A team whose parent is
    /// missing is shown at the top, so nothing disappears from the tree.
    /// </summary>
    /// <param name="teams">Every team.</param>
    /// <returns>The teams and their depth (0 at the top).</returns>
    public static IReadOnlyList<(Team Team, int Depth)> Order(IEnumerable<Team> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);

        var all = teams.ToList();
        var ids = all.Select(team => team.ItemId).ToHashSet(StringComparer.Ordinal);
        var children = all
            .Where(team => !string.IsNullOrEmpty(team.ParentId) && ids.Contains(team.ParentId))
            .ToLookup(team => team.ParentId, StringComparer.Ordinal);
        var result = new List<(Team, int)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(Team team, int depth)
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

        foreach (var root in all.Where(team => string.IsNullOrEmpty(team.ParentId) || !ids.Contains(team.ParentId)).OrderBy(team => team.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Visit(root, 0);
        }

        return result;
    }
}
