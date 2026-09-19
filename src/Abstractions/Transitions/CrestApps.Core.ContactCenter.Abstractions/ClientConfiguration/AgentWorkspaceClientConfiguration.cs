namespace CrestApps.Core.ContactCenter.ClientConfiguration;

/// <summary>
/// What the agent workspace in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// Serialized camel-cased into the page and read once on load. Every property here is a name the
/// script reads, so removing or renaming one is a change to a published contract even though no
/// compiler sees it.
/// </remarks>
public sealed class AgentWorkspaceClientConfiguration
{
    /// <summary>
    /// The fields a secure capture collects when the host names no others.
    /// </summary>
    /// <remarks>
    /// Sent to the server verbatim when a capture begins, so a typo here does not fail: it starts a
    /// capture that masks the wrong things.
    /// </remarks>
    public const string DefaultSecureCaptureFields = "CreditCardNumber,CardExpiry,CardSecurityCode";

    /// <summary>
    /// Gets or sets the URL of the Contact Center hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the workspace polls for its snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that changes the agent's presence.
    /// </summary>
    public string SetPresenceUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that accepts the current offer.
    /// </summary>
    public string AcceptOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that declines the current offer.
    /// </summary>
    public string DeclineOfferUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that pauses recording.
    /// </summary>
    public string PauseRecordingUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that resumes recording.
    /// </summary>
    public string ResumeRecordingUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this agent may pause recording at all.
    /// </summary>
    /// <remarks>
    /// Both a setting and a grant: the host decides it from the tenant's configuration and from what
    /// this caller is allowed to do, and the workspace only shows the control when both say yes.
    /// </remarks>
    public bool CanSecurePause { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pausing requires a stated reason.
    /// </summary>
    public bool RequirePauseReason { get; set; }

    /// <summary>
    /// Gets or sets the URL that begins a secure capture.
    /// </summary>
    public string BeginSecureCaptureUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this agent may begin a secure capture.
    /// </summary>
    public bool CanInitiateSecureCapture { get; set; }

    /// <summary>
    /// Gets or sets the fields a secure capture collects.
    /// </summary>
    public string SecureCaptureFields { get; set; } = DefaultSecureCaptureFields;

    /// <summary>
    /// Gets or sets the URL that opens the full completion screen for one activity.
    /// </summary>
    /// <remarks>
    /// Carries the <c>__activityId__</c> token, which the script replaces by literal text match. The
    /// spelling and casing are part of the contract, and are deliberately not the token the soft phone
    /// uses for an interaction id.
    /// </remarks>
    public string CompleteActivityUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the request token the browser sends back on every post.
    /// </summary>
    public string AntiForgeryToken { get; set; }

    /// <summary>
    /// Gets or sets the translated strings the workspace renders.
    /// </summary>
    /// <remarks>
    /// Filled by the host rather than by this suite. Translations are keyed on where the string was
    /// written as much as on the string itself, so moving these out of the host's own templates would
    /// leave every existing translation unreachable.
    /// </remarks>
    public IDictionary<string, string> Strings { get; set; }
}
