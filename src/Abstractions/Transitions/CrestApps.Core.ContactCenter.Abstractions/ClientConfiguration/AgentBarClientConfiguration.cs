namespace CrestApps.Core.ContactCenter.ClientConfiguration;

/// <summary>
/// What the docked agent bar in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// The bar rides along on every administration page so that work assigned while the agent is
/// anywhere in the host - or while their soft phone is in a window of its own - still reaches them.
/// Serialized camel-cased into the page and read once on load.
/// </remarks>
public sealed class AgentBarClientConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the Contact Center hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the bar polls for its snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that changes the agent's presence.
    /// </summary>
    /// <remarks>
    /// Sent but not read: the bar shows presence and the soft phone changes it, so that an agent has
    /// one place to say whether they are available rather than two that can disagree.
    /// </remarks>
    public string SetPresenceUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that accepts, or for a preview dial places, the current offer.
    /// </summary>
    public string AcceptOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that declines, or for a preview dial skips, the current offer.
    /// </summary>
    public string DeclineOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that applies a disposition inline.
    /// </summary>
    /// <remarks>
    /// Sent but not read; dispositioning goes through the completion screen.
    /// </remarks>
    public string CompleteUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that opens the full completion screen for one activity.
    /// </summary>
    /// <remarks>
    /// Carries the <c>__activityId__</c> token, which the script replaces by literal text match, and a
    /// return address so the agent lands back on the page they were on.
    /// </remarks>
    public string CompleteActivityUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the URL of the full agent workspace.
    /// </summary>
    public string WorkspaceUrl { get; set; }

    /// <summary>
    /// Gets or sets the request token the browser sends back on every post.
    /// </summary>
    public string AntiForgeryToken { get; set; }

    /// <summary>
    /// Gets or sets the dispositions offered inline.
    /// </summary>
    /// <remarks>
    /// Sent but not read, for the same reason as <see cref="CompleteUrl"/>.
    /// </remarks>
    public IList<AgentBarOption> Dispositions { get; set; } = [];

    /// <summary>
    /// Gets or sets the reason codes offered with a presence change.
    /// </summary>
    /// <remarks>
    /// Sent but not read, for the same reason as <see cref="SetPresenceUrl"/>.
    /// </remarks>
    public IList<AgentBarOption> ReasonCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the translated strings the bar renders.
    /// </summary>
    /// <remarks>
    /// Filled by the host rather than by this suite. Translations are keyed on where the string was
    /// written as much as on the string itself, so moving these out of the host's own templates would
    /// leave every existing translation unreachable.
    /// </remarks>
    public IDictionary<string, string> Strings { get; set; }
}
