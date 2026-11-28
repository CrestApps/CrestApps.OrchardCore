using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Builds the agent profile of an Orchard user from the manager-owned configuration, the way the agent entitlements
/// page, the agent entitlement recipe step and the AI agent tool create one. The profile is not saved.
/// </summary>
internal static class AgentProfileFactory
{
    /// <summary>
    /// Builds a new, unsaved agent profile for a user.
    /// </summary>
    /// <param name="manager">The agent profile manager.</param>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userName">The user's name.</param>
    /// <param name="configuration">What the agent may do: queues, campaigns, skills, memberships and concurrency.</param>
    /// <param name="createdUtc">When the profile is created.</param>
    /// <returns>The profile.</returns>
    public static async Task<AgentProfile> NewAsync(
        IAgentProfileManager manager,
        string userId,
        string userName,
        AgentManagedConfiguration configuration,
        DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(configuration);

        var agent = await manager.NewAsync();
        agent.UserId = userId;
        agent.UserName = userName;
        agent.Name = userId;
        agent.DisplayName = configuration.DisplayName;
        agent.MaxConcurrentInteractions = configuration.MaxConcurrentInteractions;
        agent.AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds(configuration.AllowedQueueIds);
        agent.AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds(configuration.AllowedCampaignIds);
        AgentEntitlementUtilities.ApplySkills(agent, configuration.Skills, configuration.SkillProficiencies);
        agent.QueueMemberships = AgentEntitlementUtilities.NormalizeQueueMemberships(configuration.QueueMemberships);
        agent.CreatedUtc = createdUtc;

        return agent;
    }
}
