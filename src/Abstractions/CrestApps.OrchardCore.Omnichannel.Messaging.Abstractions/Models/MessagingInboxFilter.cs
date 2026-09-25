namespace CrestApps.OrchardCore.Omnichannel.Messaging.Models;

/// <summary>
/// The inbox filter tabs, mirroring the OrchardCore content list's quick filters.
/// </summary>
public enum MessagingInboxFilter
{
    /// <summary>
    /// Every thread the caller may see.
    /// </summary>
    All,

    /// <summary>
    /// Only the threads assigned to the calling agent.
    /// </summary>
    Mine,

    /// <summary>
    /// Only the threads no specific agent has taken yet.
    /// </summary>
    Unassigned,
}
