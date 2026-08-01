namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Defines the contact-attribution state of an omnichannel activity.
/// </summary>
public enum ContactResolutionStatus
{
    /// <summary>
    /// The activity predates explicit contact resolution or does not require contact attribution.
    /// </summary>
    Unknown,

    /// <summary>
    /// No contact has been resolved for the activity.
    /// </summary>
    Unresolved,

    /// <summary>
    /// The activity has one explicitly or automatically resolved contact.
    /// </summary>
    Resolved,

    /// <summary>
    /// Multiple contacts match and an explicit selection is required.
    /// </summary>
    Ambiguous,
}
