namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes the durable provider work required to connect an accepted inbound offer to its agent.
/// </summary>
public sealed class ProviderAnswerCommandRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the assigned agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier represented by the agent profile.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier that produced the offer.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets whether a definitive connect failure should return the work to inbound routing.
    /// </summary>
    public bool ReofferOnFailure { get; set; }

    /// <summary>
    /// Gets or sets the agent leg the provider rang while the offer was still ringing, when it did. The answer then
    /// readies the caller and joins this leg instead of ringing the agent again.
    /// </summary>
    public string PreDialedAgentLegId { get; set; }

    /// <summary>
    /// Gets or sets the reservation an agent standing by for an over-dialing campaign was claimed under, when the agent
    /// was connected to an answered call without being offered it. The provider tags the agent's leg with it, so the
    /// agent's phone answers the leg at once instead of waiting to learn of the claim.
    /// </summary>
    public string StandbyReservationId { get; set; }

    /// <summary>
    /// Gets or sets how long the agent's leg may ring before the provider gives up on it, in seconds, or 0 for the
    /// provider's own default.
    /// </summary>
    public int AgentLegTimeoutSeconds { get; set; }
}
