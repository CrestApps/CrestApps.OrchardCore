namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the serialized payload for an auditable routing decision event.
/// </summary>
public sealed class ActivityRoutingDecisionEventData
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the queue item identifier.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the selected agent profile identifier.
    /// </summary>
    public string SelectedAgentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether routing selected an agent.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the human-readable routing outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the candidate scoring details.
    /// </summary>
    public IList<ActivityRoutingCandidateDecisionData> Candidates { get; set; } = [];
}
