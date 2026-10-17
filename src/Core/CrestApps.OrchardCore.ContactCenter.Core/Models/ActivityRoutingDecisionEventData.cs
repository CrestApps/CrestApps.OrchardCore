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

    /// <summary>
    /// Gets or sets how many identical, consecutive decisions the event stands for. Routing folds a failed decision
    /// that repeats the queue's last one exactly (the same record, reason and candidates) into the event that recorded
    /// it first, instead of publishing a new event on every pass. An event that stands for one decision, including
    /// every event written before decisions were coalesced, reads as one.
    /// </summary>
    public int OccurrenceCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the UTC time the last decision the event stands for was made, or <see langword="null"/> when the
    /// event stands for a single decision, made when the event occurred.
    /// </summary>
    public DateTime? LastSeenUtc { get; set; }
}
