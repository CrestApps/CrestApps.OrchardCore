namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Asks a provider to ring an agent's device for an offer that is still ringing. See
/// <see cref="IContactCenterVoiceAgentPreDialProvider"/>.
/// </summary>
public sealed class ContactCenterAgentPreDialRequest
{
    /// <summary>
    /// Gets or sets the identifier of the offer (reservation) the leg belongs to. The agent's client uses it to tell
    /// the leg apart from any other incoming call and tie it to the offer it is showing.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier the offer is for.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the caller's leg the agent leg will be joined to.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile the offer is presented to.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user the agent profile represents.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets how long the leg may ring before the provider gives up on it. It is the offer's remaining
    /// lifetime, so a leg nobody answers ends with the offer.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the provider address to ring, when the caller already resolved it. Left empty, the provider
    /// resolves the agent's device itself.
    /// </summary>
    public string AgentEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the pre-dialed leg this one replaces, when the agent's device refused that one as unavailable and is
    /// being rung again where it registered since.
    /// </summary>
    public string ReplacesAgentLegId { get; set; }
}
