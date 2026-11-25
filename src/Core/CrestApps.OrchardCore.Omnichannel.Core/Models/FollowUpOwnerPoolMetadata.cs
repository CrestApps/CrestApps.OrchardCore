namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Where the users of a follow-up owner pool come from.
/// </summary>
public enum FollowUpOwnerPoolSource
{
    /// <summary>
    /// The members of a team.
    /// </summary>
    Team,

    /// <summary>
    /// The users in one or more roles.
    /// </summary>
    Roles,

    /// <summary>
    /// A list of users.
    /// </summary>
    Users,
}

/// <summary>
/// How one user of a follow-up owner pool is picked.
/// </summary>
public enum FollowUpOwnerPoolStrategy
{
    /// <summary>
    /// The user who was given an activity longest ago.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// The user with the fewest open activities.
    /// </summary>
    FewestOpenActivities,
}

/// <summary>
/// The pool a subject action picks the owner of its follow-up from, for
/// <see cref="SubjectActionOwnerAssignmentType.Pool"/>. Stored with Put/TryGet on <see cref="SubjectAction"/>.
/// </summary>
public sealed class FollowUpOwnerPoolMetadata
{
    /// <summary>
    /// Gets or sets where the pool's users come from.
    /// </summary>
    public FollowUpOwnerPoolSource Source { get; set; }

    /// <summary>
    /// Gets or sets the team, for <see cref="FollowUpOwnerPoolSource.Team"/>.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the roles, for <see cref="FollowUpOwnerPoolSource.Roles"/>.
    /// </summary>
    public string[] RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the user ids, for <see cref="FollowUpOwnerPoolSource.Users"/>.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how one user is picked.
    /// </summary>
    public FollowUpOwnerPoolStrategy Strategy { get; set; }
}
