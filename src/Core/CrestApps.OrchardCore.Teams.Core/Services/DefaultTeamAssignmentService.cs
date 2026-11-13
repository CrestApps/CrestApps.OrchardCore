using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;

namespace CrestApps.OrchardCore.Teams.Core.Services;

/// <summary>
/// The default <see cref="ITeamAssignmentService"/>. It keeps no state of its own: whose turn it is comes from when
/// each member was last given work of the kind, which the kind's <see cref="ITeamMemberLoadSource"/> reports, so the
/// rotation is the same on every node.
/// </summary>
public sealed class DefaultTeamAssignmentService : ITeamAssignmentService
{
    private readonly ITeamMembershipService _membershipService;
    private readonly IEnumerable<ITeamMemberLoadSource> _loadSources;
    private readonly IEnumerable<ITeamMemberAvailabilitySource> _availabilitySources;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTeamAssignmentService"/> class.
    /// </summary>
    /// <param name="membershipService">The team membership service.</param>
    /// <param name="loadSources">The sources of the members' load.</param>
    /// <param name="availabilitySources">The sources of the members' availability.</param>
    public DefaultTeamAssignmentService(
        ITeamMembershipService membershipService,
        IEnumerable<ITeamMemberLoadSource> loadSources,
        IEnumerable<ITeamMemberAvailabilitySource> availabilitySources)
    {
        _membershipService = membershipService;
        _loadSources = loadSources;
        _availabilitySources = availabilitySources;
    }

    /// <inheritdoc/>
    public async Task<TeamMember> SelectAsync(TeamAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.TeamId))
        {
            return null;
        }

        var excluded = new HashSet<string>(request.ExcludedUserIds ?? [], StringComparer.Ordinal);

        // One candidate per person: a member of the team and of one of its sub-teams is considered once.
        IReadOnlyList<TeamMember> candidates = (await _membershipService.GetMembersAsync(request.TeamId, request.IncludeSubTeams, cancellationToken: cancellationToken))
            .Where(member => !string.IsNullOrEmpty(member.UserId) && !excluded.Contains(member.UserId))
            .GroupBy(member => member.UserId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (candidates.Count > 0 && request.OnlyAvailable)
        {
            candidates = await RemoveUnavailableAsync(candidates, cancellationToken);
        }

        if (candidates.Count > 0 && request.CandidateFilter is not null)
        {
            candidates = await request.CandidateFilter(candidates, cancellationToken) ?? [];
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var load = await GetLoadAsync(request.WorkKind, candidates, cancellationToken);

        TeamMemberLoad LoadOf(TeamMember member)
            => load.GetValueOrDefault(member.UserId) ?? new TeamMemberLoad();

        var eligible = request.MaxOpen is { } maxOpen
            ? candidates.Where(member => LoadOf(member).Open < maxOpen)
            : candidates;

        var roundRobin = string.Equals(request.Strategy, TeamAssignmentStrategies.RoundRobin, StringComparison.OrdinalIgnoreCase);

        // A member who never got work of the kind goes first in the rotation. The user id breaks ties, so two nodes
        // picking at the same moment pick the same person.
        var ordered = roundRobin
            ? eligible
                .OrderBy(member => LoadOf(member).LastAssignedUtc ?? DateTime.MinValue)
                .ThenBy(member => LoadOf(member).Open)
            : eligible
                .OrderBy(member => LoadOf(member).Open)
                .ThenBy(member => LoadOf(member).LastAssignedUtc ?? DateTime.MinValue);

        return ordered.ThenBy(member => member.UserId, StringComparer.Ordinal).FirstOrDefault();
    }

    private async Task<IReadOnlyList<TeamMember>> RemoveUnavailableAsync(IReadOnlyList<TeamMember> candidates, CancellationToken cancellationToken)
    {
        var userIds = candidates.Select(member => member.UserId).ToArray();
        var unavailable = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in _availabilitySources)
        {
            unavailable.UnionWith(await source.GetUnavailableAsync(userIds, cancellationToken) ?? []);
        }

        return unavailable.Count == 0 ? candidates : [.. candidates.Where(member => !unavailable.Contains(member.UserId))];
    }

    private async Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(string workKind, IReadOnlyList<TeamMember> candidates, CancellationToken cancellationToken)
    {
        var source = string.IsNullOrEmpty(workKind)
            ? null
            : _loadSources.FirstOrDefault(candidate => string.Equals(candidate.WorkKind, workKind, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return new Dictionary<string, TeamMemberLoad>();
        }

        return await source.GetLoadAsync([.. candidates.Select(member => member.UserId)], cancellationToken) ?? new Dictionary<string, TeamMemberLoad>();
    }
}
