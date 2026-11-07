using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;

namespace CrestApps.OrchardCore.Teams.Core.Services;

/// <summary>
/// Reads team members from every registered <see cref="ITeamMemberProvider"/>, and walks the team tree for the members
/// of a team's sub-teams. With no provider, every team has no members.
/// </summary>
public sealed class DefaultTeamMembershipService : ITeamMembershipService
{
    private readonly ITeamManager _teamManager;
    private readonly IEnumerable<ITeamMemberProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTeamMembershipService"/> class.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="providers">The member providers.</param>
    public DefaultTeamMembershipService(
        ITeamManager teamManager,
        IEnumerable<ITeamMemberProvider> providers)
    {
        _teamManager = teamManager;
        _providers = providers;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TeamMember>> GetMembersAsync(string teamId, bool includeSubTeams = true, DateOnly? on = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            return [];
        }

        IReadOnlyCollection<string> teamIds = includeSubTeams
            ? [.. await _teamManager.GetSubtreeIdsAsync(teamId, cancellationToken)]
            : [teamId];

        if (teamIds.Count == 0)
        {
            return [];
        }

        var members = new List<TeamMember>();

        foreach (var provider in _providers)
        {
            members.AddRange(await provider.GetMembersAsync(teamIds, cancellationToken) ?? []);
        }

        return Distinct(members, on);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var memberships = new List<TeamMember>();

        foreach (var provider in _providers)
        {
            memberships.AddRange(await provider.GetMembershipsAsync(userId, cancellationToken) ?? []);
        }

        return Distinct(memberships, on);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(string teamId, string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var memberships = await GetMembershipsAsync(userId, on, cancellationToken);

        if (memberships.Count == 0)
        {
            return false;
        }

        var subtree = await _teamManager.GetSubtreeIdsAsync(teamId, cancellationToken);

        return memberships.Any(membership => subtree.Contains(membership.TeamId));
    }

    // Keeps one membership per team and user, the first a provider gave, after leaving out those that don't count on the day.
    private static List<TeamMember> Distinct(IEnumerable<TeamMember> memberships, DateOnly? on)
        => [.. memberships
            .Where(membership => membership is not null
                && !string.IsNullOrEmpty(membership.TeamId)
                && !string.IsNullOrEmpty(membership.UserId)
                && (on is null || membership.IsEffectiveOn(on.Value)))
            .DistinctBy(membership => (membership.TeamId, membership.UserId))];
}
