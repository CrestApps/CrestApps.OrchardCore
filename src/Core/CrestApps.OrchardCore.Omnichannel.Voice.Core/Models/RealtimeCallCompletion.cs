namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// What a finished live session left for somebody else to carry out: hand the caller to an agent, or hang up.
/// </summary>
/// <remarks>
/// Plain values rather than the services that produced them, because this crosses a scope boundary. The session
/// runs inside the provider's webhook request, which the provider has usually abandoned by the time the call
/// ends; the work described here is done in a scope of its own, and anything belonging to the request scope
/// would already be gone.
/// </remarks>
public sealed class RealtimeCallCompletion
{
    /// <summary>
    /// Gets or sets the activity behind the call.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider carrying the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the call leg.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model asked to hand the caller to a live agent.
    /// </summary>
    public bool HandoffRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model reported the conversation finished.
    /// </summary>
    public bool EndCallRequested { get; set; }

    /// <summary>
    /// Gets or sets the reason the model gave for ending the call.
    /// </summary>
    public string EndCallReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live session was lost with the caller still on the line, so
    /// somebody has to take the call over rather than leave it silent.
    /// </summary>
    public bool SessionLost { get; set; }

    /// <summary>
    /// Gets or sets when the live session ended, which is when the assistant's part of the conversation ended.
    /// </summary>
    /// <remarks>
    /// Carried because the work is done later, somewhere else: a handoff's conversation ended with the session, not
    /// when the handoff was carried out and not when the caller finally hung up on the agent.
    /// </remarks>
    public DateTime? SessionEndedUtc { get; set; }
}
