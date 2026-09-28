using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The consult half of the presence manager: an agent a colleague consults during a warm transfer is on a call.
/// </summary>
public sealed partial class AgentPresenceManagerService
{
    /// <inheritdoc/>
    public async Task<AgentProfile> StartConsultWorkAsync(string agentId, AgentStateChangeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(profile.UserId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{profile.UserId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        // Only an agent routing could have offered work to. Left Available while talking to a colleague, the router
        // rings them with the next queued call in the middle of the consult; one on a break or away took the call
        // as a favour and keeps the state they chose.
        if (profile is null || profile.PresenceStatus != AgentPresenceStatus.Available)
        {
            return profile;
        }

        var previousStatus = profile.PresenceStatus;

        // Captured the way a reservation captures it, so finishing or abandoning the handover returns them here.
        profile.RequestedPresenceStatus ??= AgentPresenceStatus.Available;
        profile.PresenceRequestedUtc = null;

        var actor = context?.Actor ?? ContactCenterActor.Agent(profile.UserId);

        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.Busy, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.ConsultAnswered,
            InteractionId = context?.InteractionId,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(profile.UserId, cancellationToken),
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }
}
