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

    /// <summary>
    /// Picks a member and says why nobody was picked when no one was, so the caller can show it.
    /// </summary>
    /// <param name="request">The team, the kind of work and how to pick.</param>
    /// <param name="cancellationToken">A token to cancel the selection.</param>
    /// <returns>The member picked, or the reason nobody was.</returns>
    async Task<TeamAssignmentResult> PickAsync(TeamAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var member = await SelectAsync(request, cancellationToken);

        return new TeamAssignmentResult(member, member is null ? TeamAssignmentOutcome.NoneAvailable : TeamAssignmentOutcome.Picked);
    }
}
