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
}
