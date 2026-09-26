using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides which queues and campaigns an agent may sign in to. The default implementation is permissive (no
/// restriction); the Agent Entitlements feature replaces it with an enforcing implementation that limits access to
/// the manager-granted allow-lists on the agent profile. Consumers depend on this abstraction instead of branching
/// on a feature flag, so entitlement strategy can evolve (skills, roles, teams) without touching sign-in.
/// </summary>
public interface IAgentEntitlementPolicy
{
    /// <summary>
    /// Resolves which of the selected queues and campaigns the agent may join at sign-in. An implementation may
    /// mutate the profile's allow-lists (the permissive default grants the selection so the availability gate,
    /// which reads <see cref="AgentProfile.AllowedQueueIds"/>, still admits the work).
    /// </summary>
    (IList<string> Queues, IList<string> Campaigns) ResolveMemberships(
        AgentProfile profile,
        IEnumerable<string> selectedQueueIds,
        IEnumerable<string> selectedCampaignIds);

    /// <summary>Determines whether the agent may sign in to the queue (used to build the sign-in picker).</summary>
    bool AllowsQueue(AgentProfile profile, string queueId);

    /// <summary>Determines whether the agent may sign in to the campaign (used to build the sign-in picker).</summary>
    bool AllowsCampaign(AgentProfile profile, string campaignId);

    /// <summary>Returns the queues the agent is currently signed in to that the policy still permits.</summary>
    IList<string> GetSignedInQueueIds(AgentProfile profile);

    /// <summary>Returns the campaigns the agent is currently signed in to that the policy still permits.</summary>
    IList<string> GetSignedInCampaignIds(AgentProfile profile);
}

/// <summary>
/// The default policy: no entitlement restriction. Any agent may sign in to any queue or campaign, and the
/// selection is granted onto the profile's allow-lists so the routing and availability gate admits the work. This
/// is the behavior when the Agent Entitlements feature is disabled.
/// </summary>
public sealed class PermissiveAgentEntitlementPolicy : IAgentEntitlementPolicy
{
    /// <inheritdoc/>
    public (IList<string> Queues, IList<string> Campaigns) ResolveMemberships(
        AgentProfile profile,
        IEnumerable<string> selectedQueueIds,
        IEnumerable<string> selectedCampaignIds)
    {
        var queues = AgentEntitlementUtilities.NormalizeIds(selectedQueueIds);
        var campaigns = AgentEntitlementUtilities.NormalizeIds(selectedCampaignIds);

        profile.AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds((profile.AllowedQueueIds ?? []).Concat(queues));
        profile.AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds((profile.AllowedCampaignIds ?? []).Concat(campaigns));

        return (queues, campaigns);
    }

    /// <inheritdoc/>
    public bool AllowsQueue(AgentProfile profile, string queueId) => true;

    /// <inheritdoc/>
    public bool AllowsCampaign(AgentProfile profile, string campaignId) => true;

    /// <inheritdoc/>
    public IList<string> GetSignedInQueueIds(AgentProfile profile)
        => AgentEntitlementUtilities.NormalizeIds(profile?.QueueIds);

    /// <inheritdoc/>
    public IList<string> GetSignedInCampaignIds(AgentProfile profile)
        => AgentEntitlementUtilities.NormalizeIds(profile?.CampaignIds);
}

/// <summary>
/// The enforcing policy registered by the Agent Entitlements feature: an agent may sign in only to the queues and
/// campaigns granted on their profile, and current membership is constrained to that same grant.
/// </summary>
public sealed class EnforcingAgentEntitlementPolicy : IAgentEntitlementPolicy
{
    /// <inheritdoc/>
    public (IList<string> Queues, IList<string> Campaigns) ResolveMemberships(
        AgentProfile profile,
        IEnumerable<string> selectedQueueIds,
        IEnumerable<string> selectedCampaignIds)
        => (
            AgentEntitlementUtilities.FilterEntitled(selectedQueueIds, profile.AllowedQueueIds),
            AgentEntitlementUtilities.FilterEntitled(selectedCampaignIds, profile.AllowedCampaignIds));

    /// <inheritdoc/>
    public bool AllowsQueue(AgentProfile profile, string queueId)
        => profile?.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true;

    /// <inheritdoc/>
    public bool AllowsCampaign(AgentProfile profile, string campaignId)
        => profile?.AllowedCampaignIds?.Contains(campaignId, StringComparer.OrdinalIgnoreCase) == true;

    /// <inheritdoc/>
    public IList<string> GetSignedInQueueIds(AgentProfile profile)
        => AgentEntitlementUtilities.FilterEntitled(profile?.QueueIds, profile?.AllowedQueueIds);

    /// <inheritdoc/>
    public IList<string> GetSignedInCampaignIds(AgentProfile profile)
        => AgentEntitlementUtilities.FilterEntitled(profile?.CampaignIds, profile?.AllowedCampaignIds);
}
