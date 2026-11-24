using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Describes agent profiles for the setup tools, from an explicit list of fields: who the agent is, what they may do and
/// what they are signed in to. Presence reasons, voicemail greetings and reservations are not returned.
/// </summary>
internal sealed class ContactCenterAgentDescriber
{
    private readonly Dictionary<string, string> _queueNames;
    private readonly Dictionary<string, string> _campaignNames;

    private ContactCenterAgentDescriber(Dictionary<string, string> queueNames, Dictionary<string, string> campaignNames)
    {
        _queueNames = queueNames;
        _campaignNames = campaignNames;
    }

    /// <summary>
    /// Reads the queue and campaign names an agent refers to.
    /// </summary>
    public static async Task<ContactCenterAgentDescriber> CreateAsync(IServiceProvider services, CancellationToken cancellationToken)
        => new(
            await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken),
            await ContactCenterSetupLookups.GetCampaignNamesAsync(services, cancellationToken));

    /// <summary>
    /// Describes an agent profile.
    /// </summary>
    public object Describe(AgentProfile agent)
    {
        var proficiencies = (agent.SkillProficiencies ?? []).Where(skill => skill is not null).ToList();

        return new
        {
            id = agent.ItemId,
            userName = agent.UserName,
            displayName = ContactCenterSetupLookups.DescribeAgent(agent),
            presence = agent.PresenceStatus.ToString(),
            maxConcurrentInteractions = agent.MaxConcurrentInteractions,
            signedInQueues = Queues(agent.QueueIds),
            signedInCampaigns = Campaigns(agent.CampaignIds),
            allowedQueues = Queues(agent.AllowedQueueIds),
            allowedCampaigns = Campaigns(agent.AllowedCampaignIds),
            skills = proficiencies
                .Select(skill => new { skillId = skill.SkillId, proficiency = (int?)skill.Proficiency })
                .Concat((agent.Skills ?? [])
                    .Where(name => !proficiencies.Any(skill => string.Equals(skill.SkillId, name, StringComparison.OrdinalIgnoreCase)))
                    .Select(name => new { skillId = name, proficiency = (int?)null }))
                .ToList(),
            queueMemberships = (agent.QueueMemberships ?? [])
                .Where(membership => membership is not null)
                .Select(membership => new
                {
                    queueId = membership.QueueId,
                    queueName = _queueNames.GetValueOrDefault(membership.QueueId ?? string.Empty),
                    priority = membership.Priority,
                    delaySeconds = membership.DelaySeconds,
                })
                .ToList(),
        };
    }

    private List<object> Queues(IEnumerable<string> ids)
        => (ids ?? [])
            .Select(id => ContactCenterConstants.IsCampaignQueue(id)
                ? (object)new { id, name = (string)null, campaignQueue = true }
                : new { id, name = _queueNames.GetValueOrDefault(id ?? string.Empty), campaignQueue = false })
            .ToList();

    private List<object> Campaigns(IEnumerable<string> ids)
        => (ids ?? [])
            .Select(id => (object)new { id, name = _campaignNames.GetValueOrDefault(id ?? string.Empty) })
            .ToList();
}
