namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Identifies how the owner of a follow-up subject action is selected.
/// </summary>
public enum SubjectActionOwnerAssignmentType
{
    /// <summary>
    /// Assigns the follow-up activity to the user who completed the current activity.
    /// </summary>
    SameOwner,

    /// <summary>
    /// Assigns the follow-up activity to a configured Orchard user.
    /// </summary>
    SpecificOwner,

    /// <summary>
    /// Assigns the follow-up activity to the agent the call was placed or connected for: the agent a Power or
    /// Progressive dial reserved, or the agent a Predictive call was connected to, even when the system applied the
    /// disposition. When no call names an agent, the user who completed the activity owns it.
    /// </summary>
    CallAgent,

    /// <summary>
    /// Assigns the follow-up activity to one user of a pool -- the members of a team, the users in roles, or a list of
    /// users -- picked by round robin or by the fewest open activities. See <see cref="FollowUpOwnerPoolMetadata"/>.
    /// </summary>
    Pool,
}
