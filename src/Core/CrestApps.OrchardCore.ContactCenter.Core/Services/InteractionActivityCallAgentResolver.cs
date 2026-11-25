using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finds the agent an activity's call was placed or connected for from the call itself: the agent a Power or
/// Progressive dial reserved, or the agent a Predictive call was connected to once a person answered. The agent stays
/// on the call when nobody answers, so a follow-up of a disposition the system applied still finds them.
/// </summary>
internal sealed class InteractionActivityCallAgentResolver : IActivityCallAgentResolver
{
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentProfileManager;

    public InteractionActivityCallAgentResolver(IInteractionManager interactionManager, IAgentProfileManager agentProfileManager)
    {
        _interactionManager = interactionManager;
        _agentProfileManager = agentProfileManager;
    }

    public async Task<string> FindAgentUserIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (string.IsNullOrEmpty(interaction?.AgentId))
        {
            return null;
        }

        return (await _agentProfileManager.FindByIdAsync(interaction.AgentId, cancellationToken))?.UserId;
    }
}
