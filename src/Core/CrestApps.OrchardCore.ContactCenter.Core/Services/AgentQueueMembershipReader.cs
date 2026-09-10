using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IAgentQueueMembershipReader"/>, backed by the agent directory. An agent serves a queue
/// when the queue appears on the profile — either as a queue they are signed in to (<c>QueueIds</c>) or as a
/// manager-granted queue (<c>AllowedQueueIds</c>) — and the tenant's <see cref="IAgentEntitlementPolicy"/> still
/// permits it. Nothing here needs the Work Distribution feature, so channels that group agents by queue keep
/// working on a tenant that only has the agent directory.
/// </summary>
public sealed class AgentQueueMembershipReader : IAgentQueueMembershipReader
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentQueueMembershipReader"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile directory.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues an agent may serve.</param>
    public AgentQueueMembershipReader(
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy)
    {
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetQueueIdsForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return [];
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return [];
        }

        return GetQueueIds(agent);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(string agentId, string queueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(queueId))
        {
            return false;
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return false;
        }

        return GetQueueIds(agent).Contains(queueId, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetAgentIdsForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return [];
        }

        var members = await _agentProfileManager.GetMembersForQueueAsync(queueId, cancellationToken);

        return members
            .Where(member => !string.IsNullOrEmpty(member.ItemId) && _entitlementPolicy.AllowsQueue(member, queueId))
            .Select(member => member.ItemId)
            .ToArray();
    }

    private string[] GetQueueIds(AgentProfile agent)
    {
        return (agent.QueueIds ?? [])
            .Concat(agent.AllowedQueueIds ?? [])
            .Where(queueId => !string.IsNullOrEmpty(queueId) && _entitlementPolicy.AllowsQueue(agent, queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
