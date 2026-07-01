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
    /// Gets or sets the URL that starts a supervisor live-monitoring engagement.
    /// </summary>
    public string EngageUrl { get; set; }
}
