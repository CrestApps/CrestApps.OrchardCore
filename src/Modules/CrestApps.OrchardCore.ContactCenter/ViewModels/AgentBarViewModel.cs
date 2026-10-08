namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Carries the client configuration for the persistent docked agent bar. The bar is injected on every admin
/// page for a signed-in agent and connects to the Contact Center hub so a work assignment made while the agent
/// is anywhere in the CRM (and while the soft phone runs in a separate window) still reaches them: it alerts,
/// pops the record, and drives disposition. The values are serialized to the bar root's <c>data-config</c>
/// attribute and consumed by <c>contact-center-agent-bar.js</c>.
/// </summary>
public sealed class AgentBarViewModel
{
    /// <summary>
    /// Gets or sets the tenant-aware Contact Center hub URL.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL of the workspace state endpoint the bar polls for its snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that changes the agent's presence.
    /// </summary>
    public string SetPresenceUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that accepts (or, for a preview dial, dials) the current offer.
    /// </summary>
    public string AcceptOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that declines (or, for a preview dial, skips) the current offer.
    /// </summary>
    public string DeclineOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that applies a quick disposition inline from the bar.
    /// </summary>
    public string CompleteUrl { get; set; }

    /// <summary>
    /// Gets or sets the template URL that opens the full complete-activity screen for a given activity. The
    /// <c>__activityId__</c> token is replaced with the activity identifier and a return URL to the current
    /// page is embedded so the agent lands back where they were after completing.
    /// </summary>
    public string CompleteActivityUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the URL of the full agent workspace, linked from the bar for the complete desktop.
    /// </summary>
    public string WorkspaceUrl { get; set; }

    /// <summary>
    /// Gets or sets the request verification token used for the bar's POST actions.
    /// </summary>
    public string AntiForgeryToken { get; set; }

    /// <summary>
    /// Gets or sets the quick-disposition options offered inline for wrap-up.
    /// </summary>
    public IList<WorkspaceLookupViewModel> Dispositions { get; set; } = [];

    /// <summary>
    /// Gets or sets the presence reason codes offered in the bar presence menu.
    /// </summary>
    public IList<WorkspaceLookupViewModel> ReasonCodes { get; set; } = [];
}
