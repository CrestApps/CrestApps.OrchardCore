namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the complete live state of the agent desktop returned by the workspace state endpoint. The
/// client renders itself from this snapshot on load and refreshes it after real-time events, offer
/// acceptance, and wrap-up so the desktop always reflects the authoritative server state.
/// </summary>
public sealed class AgentWorkspaceStateViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the signed-in Orchard user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile, when one exists for the user.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an agent profile exists for the user.
    /// </summary>
    public bool HasProfile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent is signed in to at least one queue or campaign.
    /// </summary>
    public bool IsSignedIn { get; set; }

    /// <summary>
    /// Gets or sets the agent's current presence.
    /// </summary>
    public WorkspacePresenceViewModel Presence { get; set; } = new();

    /// <summary>
    /// Gets or sets the live depth of the queues the agent is signed in to.
    /// </summary>
    public IList<WorkspaceQueueStatViewModel> Queues { get; set; } = [];

    /// <summary>
    /// Gets or sets the work item currently offered to the agent, or <see langword="null"/> when none is pending.
    /// </summary>
    public WorkspaceOfferViewModel Offer { get; set; }

    /// <summary>
    /// Gets or sets the interaction the agent is currently handling, or <see langword="null"/> when idle.
    /// </summary>
    public WorkspaceActiveInteractionViewModel ActiveInteraction { get; set; }

    /// Gets or sets the agent's most recent interactions.
    /// </summary>
    public IList<WorkspaceHistoryEntryViewModel> RecentHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the authoritative server UTC time, used by the client to align local timers.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
