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
}
