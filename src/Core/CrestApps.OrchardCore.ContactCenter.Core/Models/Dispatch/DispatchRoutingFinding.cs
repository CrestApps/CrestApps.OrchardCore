namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

/// <summary>
/// The most recent routing attempt for a queue that found no agent to take its next item, taken from the routing
/// decision the dispatcher recorded.
/// </summary>
public sealed class DispatchRoutingFinding
{
    /// <summary>
    /// Gets or sets when routing made the decision.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason routing gave for assigning nobody.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the number of agents routing considered.
    /// </summary>
    public int CandidateCount { get; set; }

    /// <summary>
    /// Gets or sets, for the agents routing turned away, how many were turned away for each reason, most common first.
    /// </summary>
    public IList<DispatchCandidateReason> CandidateReasons { get; set; } = [];
}

/// <summary>
/// A reason routing gave for turning agents away, and how many it turned away for it.
/// </summary>
public sealed class DispatchCandidateReason
{
    /// <summary>
    /// Gets or sets the reason routing gave.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the number of agents turned away for the reason.
    /// </summary>
    public int AgentCount { get; set; }
}
