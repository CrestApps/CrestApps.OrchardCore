using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Answers who is in a team and which teams a person is in, from every registered <see cref="ITeamMemberProvider"/>.
/// A team's members include the members of the teams under it, the way a manager of a team also manages its sub-teams.
/// </summary>
public interface ITeamMembershipService
{
    /// <summary>
    /// Gets the members of a team.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="includeSubTeams">Whether the members of the teams under it count.</param>
    /// <param name="on">The day the memberships must count on, or <see langword="null"/> for every membership.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The memberships, each once per team and user.</returns>
    Task<IReadOnlyList<TeamMember>> GetMembersAsync(string teamId, bool includeSubTeams = true, DateOnly? on = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the teams a user is in.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="on">The day the memberships must count on, or <see langword="null"/> for every membership.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user's memberships, each once per team.</returns>
    Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, DateOnly? on = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user is in a team, or in a team under it.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="userId">The user.</param>
    /// <param name="on">The day the membership must count on, or <see langword="null"/> for any day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the user is a member.</returns>
    Task<bool> IsMemberAsync(string teamId, string userId, DateOnly? on = null, CancellationToken cancellationToken = default);
}
