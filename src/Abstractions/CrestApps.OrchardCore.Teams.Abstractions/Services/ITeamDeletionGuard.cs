using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Lets a module that uses teams refuse to have one deleted while the module still depends on it, such as while the
/// team has members. Register implementations as scoped services; <see cref="ITeamManager.GetDeletionBlockersAsync"/>
/// asks each of them.
/// </summary>
public interface ITeamDeletionGuard
{
    /// <summary>
    /// Gets why the team can't be deleted, or <see langword="null"/> when nothing in this module holds it.
    /// </summary>
    /// <param name="team">The team about to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reason, shown to the person deleting the team, or <see langword="null"/>.</returns>
    Task<string> GetBlockerAsync(Team team, CancellationToken cancellationToken = default);
}
