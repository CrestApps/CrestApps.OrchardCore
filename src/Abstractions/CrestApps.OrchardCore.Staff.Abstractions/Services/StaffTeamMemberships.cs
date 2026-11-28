using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// The rules for changing a staff member's team memberships, shared by the staff editor's Teams card and the AI tools: a
/// manager can only add people to the teams they manage (memberships already there stay as they are), and the main team
/// becomes the first membership.
/// </summary>
public static class StaffTeamMemberships
{
    /// <summary>
    /// Gets whether new memberships name a team the person changing them doesn't manage.
    /// </summary>
    /// <param name="scope">The scope of the person changing the memberships.</param>
    /// <param name="staffMember">The staff member, with the memberships as stored.</param>
    /// <param name="memberships">The new memberships.</param>
    /// <returns><see langword="true"/> when a membership not already there is for a team outside the scope.</returns>
    public static bool AddsTeamOutsideScope(StaffScope scope, StaffMember staffMember, IEnumerable<TeamMembership> memberships)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(staffMember);

        if (scope.All)
        {
            return false;
        }

        var existing = staffMember.TeamMemberships.Select(membership => membership.TeamId).ToHashSet(StringComparer.Ordinal);

        return (memberships ?? []).Any(membership => !existing.Contains(membership.TeamId) && !scope.CoversTeam(membership.TeamId));
    }

    /// <summary>
    /// Replaces a staff member's memberships; the first becomes the main team.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="memberships">The memberships.</param>
    public static void Apply(StaffMember staffMember, IList<TeamMembership> memberships)
    {
        ArgumentNullException.ThrowIfNull(staffMember);

        staffMember.TeamMemberships = memberships ?? [];
        staffMember.PrimaryTeamId = staffMember.TeamMemberships.FirstOrDefault()?.TeamId;
    }
}
