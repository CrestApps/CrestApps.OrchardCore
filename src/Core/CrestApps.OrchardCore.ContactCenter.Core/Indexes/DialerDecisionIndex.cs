using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents one routing decision or dial suppression in the dialer decision log, so the decisions can be filtered,
/// grouped and counted without reading the JSON payload of every event.
/// </summary>
/// <remarks>
/// One row is written for each <c>RoutingDecisionMade</c> and <c>DialSuppressed</c> event. A routing event that stands
/// for several identical, consecutive decisions carries how many in <see cref="OccurrenceCount"/> and when the last
/// one was made in <see cref="LastSeenUtc"/>.
/// </remarks>
public sealed class DialerDecisionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the event type: <c>RoutingDecisionMade</c> or <c>DialSuppressed</c>.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the decision was made for, which is a campaign's virtual queue for
    /// campaign work.
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
    /// Gets or sets the identifier of the queue item the decision was about, for a routing decision.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent routing selected, or the agent reserved for a suppressed attempt.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing assigned the work. A suppression is never a success.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the reason: the routing outcome text for a routing decision, or the suppression reason name for a
    /// suppression.
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
