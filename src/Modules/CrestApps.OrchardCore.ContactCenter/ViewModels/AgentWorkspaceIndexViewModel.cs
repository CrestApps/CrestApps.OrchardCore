namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Provides the static bootstrap data the agent desktop page needs before the real-time client connects:
/// the endpoint URLs it calls, the presence options the agent can choose, and whether the current user
/// may also open the supervisor dashboard.
/// </summary>
public sealed class AgentWorkspaceIndexViewModel
{
    /// <summary>
    /// Gets or sets the display name shown for the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may open the supervisor dashboard.
    /// </summary>
    public bool CanMonitor { get; set; }

    /// <summary>
    /// Gets or sets the URL of the Contact Center real-time hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that returns the live workspace state snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that changes the agent presence.
    /// </summary>
    public string SetPresenceUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that accepts an offered inbound call and connects the media.
    /// </summary>
    public string AcceptOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that declines an offered inbound call and re-offers it.
    /// </summary>
    public string DeclineOfferUrl { get; set; }

    /// Gets or sets the URL of the supervisor dashboard, when the current user may open it.
    /// </summary>
    public string SupervisorDashboardUrl { get; set; }

    /// <summary>
    /// Gets or sets the not-ready presence options (status and label) the agent can choose, seeded from
    /// the configured agent state reason codes.
    /// </summary>
    public IList<WorkspaceLookupViewModel> ReasonCodes { get; set; } = [];
}
