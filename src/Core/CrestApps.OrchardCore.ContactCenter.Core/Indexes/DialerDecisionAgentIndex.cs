using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents one agent named by a decision in the dialer decision log: each candidate routing weighed, the agent it
/// selected, or the agent reserved for a suppressed attempt. It answers "which decisions were made about this agent?"
/// with one indexed read.
/// </summary>
/// <remarks>
/// The decision's own columns are repeated on every row, so a read filtered by agent needs no join.
/// </remarks>
public sealed class DialerDecisionAgentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing found the agent eligible for the work.
    /// </summary>
    public bool IsEligible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent was the one routing selected, or the one reserved for a
    /// suppressed attempt.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets the event type: <c>RoutingDecisionMade</c> or <c>DialSuppressed</c>.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the decision was made for.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the campaign the decided record belongs to, when known.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity (the record) the decision was about.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing assigned the work.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded on the decision.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets how many identical, consecutive decisions the event stands for.
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the first decision the event stands for was made.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the last decision the event stands for was made.
    /// </summary>
    public DateTime LastSeenUtc { get; set; }
}
