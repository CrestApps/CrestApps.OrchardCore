using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Provides the static bootstrap data the supervisor dashboard page needs before the real-time client
/// connects: the hub and state endpoint URLs.
/// </summary>
public sealed class SupervisorDashboardIndexViewModel
{
    /// <summary>
    /// Gets or sets the URL of the Contact Center real-time hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that returns the live supervisor dashboard state snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that explains why each queue's and campaign's pending work is or is not being handed to agents.
    /// </summary>
    public string DispatchUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that returns the dialer banner, or <see langword="null"/> when the Paced Dialing feature is off.
    /// </summary>
    public string DialerUrl { get; set; }

    /// <summary>
    /// Gets or sets the dialer banner the page opens with, or <see langword="null"/> when the Paced Dialing feature is off.
    /// </summary>
    public DialerPacingBanner DialerBanner { get; set; }

    /// <summary>
    /// Gets or sets the URL that starts a supervisor live-monitoring engagement.
    /// </summary>
    public string EngageUrl { get; set; }

    /// <summary>
    /// Gets or sets the URLs of the supervisor interventions, by name: stop, switch, takeover, endCall, transfer,
    /// recording, agentState and message.
    /// </summary>
    public IDictionary<string, string> InterventionUrls { get; set; } = new Dictionary<string, string>();
}
