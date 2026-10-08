namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Tells the agent's own screens that the call they are on changed: it started ringing, connected, was held or
/// resumed, or ended. The screens read the interaction itself from the workspace state; this only says it moved.
/// </summary>
/// <remarks>
/// Without it the agent's screens learnt about a call only from offers and presence changes. An outbound dialer call
/// is accepted before it is dialed, so the one refresh that acceptance caused found the call not placed yet, and
/// nothing asked again until the call ended and wrap-up changed the agent's presence.
/// </remarks>
public sealed class AgentInteractionNotification
{
    /// <summary>
    /// Gets or sets the identifier of the interaction that changed.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of the agent on the interaction.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the agent-profile identifier of the agent on the interaction.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the domain event that reported the change, such as <c>DialStarted</c> or <c>CallConnected</c>.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the interaction status after the change, expressed as its stable name.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the interaction direction, expressed as its stable name.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the interaction is for, when it has one.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent's screens should open the activity's record now. It is set when
    /// the agent is connected to a call the dialer placed for them: until then the call was the dialer's, and nothing
    /// pops on the agent's screen.
    /// </summary>
    public bool AutoOpenActivity { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the change was broadcast.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
