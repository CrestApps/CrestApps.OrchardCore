namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Everything a manager decides about an agent: which queues and campaigns they may sign into, how well they
/// hold each skill, and how they prefer to serve the queues they are entitled to. Set as one unit so the
/// entitlement screen and an import write the same shape and prune live membership the same way.
/// </summary>
public sealed class AgentEntitlements
{
    /// <summary>
    /// Gets the queues the agent may sign into.
    /// </summary>
    public IEnumerable<string> AllowedQueueIds { get; init; }

    /// <summary>
    /// Gets the campaigns the agent may sign into.
    /// </summary>
    public IEnumerable<string> AllowedCampaignIds { get; init; }

    /// <summary>
    /// Gets how well the agent holds each skill. The skill tags a queue requires are derived from these, so a
    /// skill listed here is a skill the agent has.
    /// </summary>
    public IEnumerable<AgentSkill> SkillProficiencies { get; init; }

    /// <summary>
    /// Gets how the agent serves each queue they belong to: which queues they prefer and how long a caller
    /// waits before they are offered work from a queue they only back up.
    /// </summary>
    public IEnumerable<AgentQueueMembership> QueueMemberships { get; init; }
}
