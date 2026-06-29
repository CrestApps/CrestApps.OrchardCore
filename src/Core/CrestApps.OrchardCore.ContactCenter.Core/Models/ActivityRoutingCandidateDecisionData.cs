namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents one candidate captured in an auditable routing decision event.
/// </summary>
public sealed class ActivityRoutingCandidateDecisionData
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier linked to the agent.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the candidate was eligible.
    /// </summary>
    public bool IsEligible { get; set; }

    /// <summary>
    /// Gets or sets the final routing score.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets the explainable routing reasons.
    /// </summary>
    public IList<string> Reasons { get; set; } = [];
}
