using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages the team hierarchy. Writes run the catalog handlers, which validate the hierarchy and keep each team's
/// Workforce calendar.
/// </summary>
public interface ITeamManager : ICatalogManager<WorkforceTeam>
{
    /// <summary>
    /// Lists every team, ordered for a tree: each team followed by its sub-teams, with its depth.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams and their depth (0 at the top).</returns>
    Task<IReadOnlyList<(WorkforceTeam Team, int Depth)>> ListTreeAsync(CancellationToken cancellationToken = default);

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
    Task<IReadOnlyList<WorkforceTeam>> GetAncestorsAsync(string teamId, CancellationToken cancellationToken = default);
}
