using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Manages the team hierarchy. Writes run the catalog handlers, which validate the hierarchy and let other modules keep
/// their own data for each team.
/// </summary>
public interface ITeamManager : ICatalogManager<Team>
{
    /// <summary>
    /// Lists every team, ordered for a tree: each team followed by its sub-teams, with its depth.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams and their depth (0 at the top).</returns>
    Task<IReadOnlyList<(Team Team, int Depth)>> ListTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ids of a team and every team under it.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The team id and its descendants' ids.</returns>
    Task<IReadOnlySet<string>> GetSubtreeIdsAsync(string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a team's parent, its parent's parent and so on, nearest first.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ancestors.</returns>
    Task<IReadOnlyList<Team>> GetAncestorsAsync(string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets why a team can't be deleted: it still has sub-teams, or a module that uses it refuses through an
    /// <see cref="ITeamDeletionGuard"/>.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reasons, or an empty list when the team can be deleted.</returns>
    Task<IReadOnlyList<string>> GetDeletionBlockersAsync(Team team, CancellationToken cancellationToken = default);
}
