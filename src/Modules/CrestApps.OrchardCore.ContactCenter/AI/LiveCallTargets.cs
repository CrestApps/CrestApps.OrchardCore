using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// The kinds of live call a supervisor acts on.
/// </summary>
internal enum LiveCallType
{
    /// <summary>
    /// A Contact Center call an agent is on, named by its interaction.
    /// </summary>
    AgentCall,

    /// <summary>
    /// An agent's own phone call (a number dialed from the keypad, or an extension call), named by its phone-call key.
    /// </summary>
    PhoneCall,

    /// <summary>
    /// A call an AI voice agent is holding, named by its activity.
    /// </summary>
    AICall,
}

/// <summary>
/// A live call a supervisor may act on.
/// </summary>
/// <param name="Type">The kind of call.</param>
/// <param name="Id">The interaction identifier, the phone-call key or the activity identifier.</param>
/// <param name="Activity">The AI call's activity.</param>
/// <param name="AICall">The AI call.</param>
internal sealed record LiveCallTarget(LiveCallType Type, string Id, OmnichannelActivity Activity = null, AILiveCall AICall = null);

/// <summary>
/// Finds the live AI call or the agent a supervision tool acts on, and refuses a call outside the supervisor's scope.
/// </summary>
/// <remarks>
/// Agent calls and agents' own phone calls are scoped as the live dashboard scopes them (<see cref="SupervisorCallAccess"/>).
/// The dashboard's AI call actions check only the permission; whether queue and campaign scoping applies to AI calls is
/// an open owner decision (SEC-P7), so these tools apply the strictest rule the existing code has: an AI call is the
/// supervisor's only when they oversee its campaign's queue, so an AI call with no campaign is no supervisor's.
/// </remarks>
internal static class LiveCallTargets
{
    /// <summary>
    /// Finds a live AI voice call in the supervisor's scope.
    /// </summary>
    /// <returns>The call, or the sentence that says why there is none.</returns>
    public static async Task<(LiveCallTarget Target, string Error)> FindAICallAsync(
        IServiceProvider services,
        ClaimsPrincipal user,
        string supervisorId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var activity = string.IsNullOrEmpty(activityId)
            ? null
            : await services.GetRequiredService<IOmnichannelActivityStore>().FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) ||
            !activity.TryGet<AILiveCall>(out var liveCall) ||
            !await IsAICallInScopeAsync(services, user, supervisorId, activity, cancellationToken))
        {
            return (null, $"No live AI call you supervise has the id '{activityId}'. Use listLiveAIVoiceCalls to find it.");
        }

        return (new LiveCallTarget(LiveCallType.AICall, activity.ItemId, activity, liveCall), null);
    }

    /// <summary>
    /// Determines whether an AI call is in the supervisor's scope: they oversee its campaign's queue.
    /// </summary>
    /// <returns><see langword="true"/> when the supervisor may see and act on the call.</returns>
    public static async Task<bool> IsAICallInScopeAsync(
        IServiceProvider services,
        ClaimsPrincipal user,
        string supervisorId,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity?.CampaignId))
        {
            return false;
        }

        var authorization = services.GetService<ISupervisorQueueAuthorizationService>();

        return authorization is not null &&
            await authorization.IsAuthorizedAsync(user, supervisorId, ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId), cancellationToken);
    }

    /// <summary>
    /// Finds an agent by agent id or user name.
    /// </summary>
    /// <returns>The agent, or <see langword="null"/>.</returns>
    public static async Task<AgentProfile> FindAgentAsync(IServiceProvider services, string agent, CancellationToken cancellationToken)
    {
        var agents = services.GetRequiredService<IAgentProfileManager>();
        var profile = await agents.FindByIdAsync(agent, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        var matches = (await agents.GetAllAsync(cancellationToken))
            .Where(candidate => candidate is not null &&
                (string.Equals(candidate.UserName, agent, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.UserId, agent, StringComparison.Ordinal)))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }
}
