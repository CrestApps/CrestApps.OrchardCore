namespace CrestApps.OrchardCore.Teams.Models;

/// <summary>
/// A request to pick the member of a team who should take a piece of work.
/// </summary>
public sealed class TeamAssignmentRequest
{
    /// <summary>
    /// Gets or sets the team whose members are considered.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets whether members of the team's sub-teams are considered too.
    /// </summary>
    public bool IncludeSubTeams { get; set; } = true;

    /// <summary>
    /// Gets or sets the users considered instead of a team's members, for work whose candidates come from somewhere
    /// else, such as the agents signed in to a queue or the users in a role. When set, <see cref="TeamId"/> and
    /// <see cref="IncludeSubTeams"/> are not used, and the same availability, filter, load and strategy rules pick one
    /// of them. <see langword="null"/> uses the team.
    /// </summary>
    public IReadOnlyCollection<string> CandidateUserIds { get; set; }

    /// <summary>
    /// Gets or sets the kind of work, an open string the module that keeps the work names (such as <c>Ticket</c>). The
    /// members' load is read from the <see cref="Services.ITeamMemberLoadSource"/> of this kind.
    /// </summary>
    public string WorkKind { get; set; }

    /// <summary>
    /// Gets or sets how the member is picked, one of <see cref="TeamAssignmentStrategies"/>.
    /// </summary>
    public string Strategy { get; set; } = TeamAssignmentStrategies.LeastLoaded;

    /// <summary>
    /// Gets or sets the most open items of the kind a member may hold and still be picked, or <see langword="null"/> for
    /// no limit.
    /// </summary>
    public int? MaxOpen { get; set; }

    /// <summary>
    /// Gets or sets whether members who aren't available now (off shift, absent, signed out), as the registered
    /// <see cref="Services.ITeamMemberAvailabilitySource"/>s report, are left out.
    /// </summary>
    public bool OnlyAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets the names of the availability sources honored (<see cref="Services.ITeamMemberAvailabilitySource.Name"/>),
    /// or <see langword="null"/> for every registered source. An empty list honors none.
    /// </summary>
    public IReadOnlyCollection<string> AvailabilitySources { get; set; }

    /// <summary>
    /// Gets or sets the users never picked, such as the person the work is being taken from.
    /// </summary>
    public IReadOnlyCollection<string> ExcludedUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a filter the caller applies to the candidates before one is picked, such as keeping the members
    /// who hold a required skill. Optional.
    /// </summary>
    public Func<IReadOnlyList<TeamMember>, CancellationToken, Task<IReadOnlyList<TeamMember>>> CandidateFilter { get; set; }
}
