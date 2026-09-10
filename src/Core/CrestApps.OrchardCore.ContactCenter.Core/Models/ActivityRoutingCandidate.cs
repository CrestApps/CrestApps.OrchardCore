namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an agent considered during a Contact Center routing decision.
/// </summary>
public sealed class ActivityRoutingCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRoutingCandidate"/> class.
    /// </summary>
    /// <param name="agent">The agent profile being scored.</param>
    public ActivityRoutingCandidate(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        Agent = agent;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRoutingCandidate"/> class from an availability
    /// snapshot, so a strategy can read the candidate's current load without querying for it again.
    /// </summary>
    /// <param name="availability">The availability snapshot the candidate was drawn from.</param>
    public ActivityRoutingCandidate(AgentAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(availability.Agent);

        Agent = availability.Agent;
        Availability = availability;
    }

    /// <summary>
    /// Gets the agent profile being considered.
    /// </summary>
    public AgentProfile Agent { get; }

    /// <summary>
    /// Gets the availability snapshot the candidate was drawn from, carrying the agent's active interaction
    /// count and remaining capacity. It is the counts the caller already read, so a strategy that needs them
    /// does not issue a query per candidate. It is <see langword="null"/> only when a caller built the
    /// candidate from a bare profile.
    /// </summary>
    public AgentAvailability Availability { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the candidate is eligible for the queued item.
    /// </summary>
    public bool IsEligible { get; set; } = true;

    /// <summary>
    /// Gets or sets the aggregate routing score assigned by routing strategies.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets the explainable reasons contributed by routing strategies.
    /// </summary>
    public IList<string> Reasons { get; } = [];

    /// <summary>
    /// Adds an explainable routing reason to the candidate.
    /// </summary>
    /// <param name="reason">The routing reason.</param>
    public void AddReason(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            Reasons.Add(reason);
        }
    }
}
