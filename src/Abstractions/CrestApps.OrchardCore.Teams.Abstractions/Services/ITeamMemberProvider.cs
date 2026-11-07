using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Gives the members of teams that a module keeps, such as the workers of Workforce. Register one with
/// <see cref="TeamsServiceCollectionExtensions.AddTeamMemberProvider{TProvider}"/>; Teams never names the module.
/// </summary>
public interface ITeamMemberProvider
{
    /// <summary>
    /// Gets the members of teams, every membership with its dates.
    /// </summary>
    /// <param name="teamIds">The teams.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The memberships in those teams.</returns>
    Task<IReadOnlyList<TeamMember>> GetMembersAsync(IReadOnlyCollection<string> teamIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the teams a user belongs to, every membership with its dates.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user's memberships.</returns>
    Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default);
}
