using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The compact record of one pacing cycle of a campaign queue: when it started and completed, what it decided, and the
/// last call it placed and assigned.
/// </summary>
/// <remarks>
/// <para>
/// The record is written on a scope of its own after the cycle ends, never inside the cycle's transaction, so a cycle
/// that is rolled back, abandoned at its time budget or that throws is on record as such rather than leaving no trace.
/// </para>
/// <para>
/// Consecutive cycles of one queue that ended the same way without placing a call (nothing due, no agents, held back,
/// lock not acquired) are folded into one record for up to an hour: <see cref="CycleCount"/> counts them,
/// <see cref="FirstStartedUtc"/> is when the first began and <see cref="StartedUtc"/> and <see cref="CompletedUtc"/>
/// describe the latest. A queue that keeps finding no agent therefore adds one record an hour, not one a minute.
/// </para>
/// </remarks>
public sealed class DialerPacingCycle : CatalogItem
{
    /// <summary>
    /// Gets or sets the campaign queue the cycle paced.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign whose queue the cycle paced.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile the cycle paced the queue with, or <see langword="null"/> when the cycle ended
    /// before a profile was known.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets how the cycle ended.
    /// </summary>
    public DialerPacingCycleOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets a short description of why the cycle ended as it did, such as the reason it held back or the error it
    /// failed with.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets how many calls the cycle placed and committed.
    /// </summary>
    public int CallsPlaced { get; set; }

    /// <summary>
    /// Gets or sets how many consecutive cycles that ended the same way this record stands for.
    /// </summary>
    public int CycleCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets when the first of the cycles this record stands for started.
    /// </summary>
    public DateTime FirstStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the latest cycle started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the latest cycle ended.
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many routing decisions in the cycle assigned a record to an agent.
    /// </summary>
    public int RoutingSucceeded { get; set; }

    /// <summary>
    /// Gets or sets how many routing decisions in the cycle found no agent to take a record.
    /// </summary>
    public int RoutingNoAgent { get; set; }

    /// <summary>
    /// Gets or sets how many attempts the compliance gate suppressed in the cycle.
    /// </summary>
    public int Suppressed { get; set; }

    /// <summary>
    /// Gets or sets the interaction of the last call the cycle placed.
    /// </summary>
    public string LastPlacedInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the record (CRM activity) of the last call the cycle placed.
    /// </summary>
    public string LastPlacedActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the agent the last call the cycle placed was dialed for, or <see langword="null"/> for a call placed
    /// before any agent was reserved.
    /// </summary>
    public string LastPlacedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent routing last assigned a record to in the cycle.
    /// </summary>
    public string LastAssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the record (CRM activity) routing last assigned to an agent in the cycle.
    /// </summary>
    public string LastAssignedActivityItemId { get; set; }
}
