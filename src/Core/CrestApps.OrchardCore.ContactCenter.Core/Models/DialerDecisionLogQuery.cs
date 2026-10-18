namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the filter applied when reading the dialer decision log. Every value left empty matches every decision,
/// and the values combine with a logical AND.
/// </summary>
public sealed class DialerDecisionLogQuery
{
    /// <summary>
    /// Gets or sets the inclusive UTC start of the period. A decision is in the period when the last time it was made
    /// is at or after this instant.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive UTC end of the period. A decision is in the period when the first time it was made is
    /// at or before this instant.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the event type to read: <c>RoutingDecisionMade</c> or <c>DialSuppressed</c>.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile identifier. Only decisions that name the agent match: routing decisions that
    /// weighed or selected them, and suppressions of an attempt reserved for them.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity (the record).
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the reason, exactly as it is recorded: the routing outcome text, or the suppression reason name.
    /// </summary>
    public string Reason { get; set; }
}
