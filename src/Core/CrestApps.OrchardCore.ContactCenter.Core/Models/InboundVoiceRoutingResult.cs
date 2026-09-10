namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of routing an inbound voice event through the Contact Center.
/// </summary>
public sealed class InboundVoiceRoutingResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the inbound call was offered to an agent.
    /// </summary>
    public bool Routed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the inbound call is waiting in a Contact Center queue.
    /// </summary>
    public bool Queued { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction created for the inbound call.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity created for the inbound call.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the inbound call was placed in, when one was resolved.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the inbound call was offered to, when an agent was reserved.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets a human-readable explanation of the routing outcome, used for diagnostics.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the stable machine-readable reason code for a terminal routing outcome.
    /// </summary>
    public string ReasonCode { get; set; }
}
