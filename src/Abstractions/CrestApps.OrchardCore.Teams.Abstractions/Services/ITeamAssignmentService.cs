using CrestApps.OrchardCore.Teams.Models;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Picks the member of a team who should take a piece of work: in turn or by least open work, among the members who
/// have a login, are available and pass the caller's filter. Any module that hands work to a team uses it instead of
/// writing its own rotation.
/// </summary>
public interface ITeamAssignmentService
{
    /// <summary>
    /// Picks a member.
    /// </summary>
    /// <param name="request">The team, the kind of work and how to pick.</param>
    /// <param name="cancellationToken">A token to cancel the selection.</param>
    /// <returns>The member picked, or <see langword="null"/> when nobody qualifies.</returns>
    Task<TeamMember> SelectAsync(TeamAssignmentRequest request, CancellationToken cancellationToken = default);
}
