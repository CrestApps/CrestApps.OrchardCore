namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents an outbound Contact Center dial request.
/// </summary>
public sealed class ContactCenterDialRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier being dialed.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier for this attempt.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the stable idempotency identifier for this provider command.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets the reserved agent identifier, when the dialing mode requires an agent before dialing.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier represented by the reserved agent.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier the activity belongs to.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier the activity belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the destination address to dial.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier to present when supported.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata for the dial request.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
