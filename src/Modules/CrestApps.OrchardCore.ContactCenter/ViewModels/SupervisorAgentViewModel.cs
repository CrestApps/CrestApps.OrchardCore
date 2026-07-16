namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the live state of a single agent on the supervisor dashboard agent board.
/// </summary>
public sealed class SupervisorAgentViewModel
{
    /// <summary>
    /// Gets or sets the Contact Center agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user the agent represents.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the agent's current presence status name.
    /// </summary>
    public string PresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional reason code associated with the agent's current presence status.
    /// </summary>
    public string PresenceReason { get; set; }

    /// <summary>
    /// Gets or sets the number of queues the agent is signed in to.
    /// </summary>
    public int QueueCount { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions the agent is currently handling.
    /// </summary>
    public int ActiveInteractions { get; set; }

    /// <summary>
    /// Gets or sets the currently live interaction identifier, when supervisor engagement is possible.
    /// </summary>
    public string ActiveInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the executable supervisor engagement modes available for the active interaction.
    /// </summary>
    public IList<string> AvailableMonitoringModes { get; set; } = [];
}
