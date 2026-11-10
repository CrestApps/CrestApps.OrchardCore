using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The work half of the presence manager: moving an agent into wrap-up when a handled call ends, and back to a
/// ready state when the work is done.
/// </summary>
public sealed partial class AgentPresenceManagerService
{
    /// <inheritdoc/>
    public async Task<AgentProfile> ConnectHeldAgentAsync(string agentId, AgentStateChangeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null || !IsHeldForDial(profile, context?.ReservationId))
        {
            return profile;
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

        // Connected once is enough: a second report of the same connect, or one arriving after the agent was released,
        // finds nothing holding them.
        if (profile is null || !IsHeldForDial(profile, context?.ReservationId))
        {
            return profile;
        }

        var previousStatus = profile.PresenceStatus;
        var holdReservationId = profile.DialHoldReservationId;
        var actor = context?.Actor ?? ContactCenterActor.System;

        // On a call from the moment they are connected, not from the dial: the ringing, and any answer screened as a
        // machine, were the dialer's time, not a call the agent was on.
        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.Busy, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.Connected,
            InteractionId = context?.InteractionId,
            ReservationId = holdReservationId,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(profile.UserId, cancellationToken),
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }

    private static bool IsHeldForDial(AgentProfile profile, string reservationId)
        => profile.PresenceStatus == AgentPresenceStatus.Reserved &&
            !string.IsNullOrEmpty(profile.DialHoldReservationId) &&
            (string.IsNullOrEmpty(reservationId) || string.Equals(profile.DialHoldReservationId, reservationId, StringComparison.Ordinal));

    /// <inheritdoc/>
    public Task<AgentProfile> StartWrapUpAsync(string agentId, CancellationToken cancellationToken = default)
        => StartWrapUpAsync(agentId, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AgentProfile> StartWrapUpAsync(string agentId, AgentStateChangeContext context, CancellationToken cancellationToken = default)
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

        if (profile is null)
        {
            return null;
        }

        // Wrap-up (after-call work) only applies once an agent has actually handled a call, which is exactly when
        // they are Busy. An agent who was merely offered a call they never accepted -- an unanswered or expired
        // offer -- is Reserved (or already back in a ready state), never Busy; forcing them into wrap-up would
        // strand them there because there is no accepted call to disposition and nothing to move them back out.
        if (profile.PresenceStatus != AgentPresenceStatus.Busy)
        {
            return profile;
        }

        var previousStatus = profile.PresenceStatus;

        profile.ActiveReservationId = null;

        var actor = context?.Actor ?? ContactCenterActor.System;

        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.WrapUp, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.WrapUpStarted,
            InteractionId = context?.InteractionId,
            ReservationId = context?.ReservationId,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(profile.UserId, cancellationToken),
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public Task<AgentProfile> CompleteWorkAsync(string agentId, CancellationToken cancellationToken = default)
        => CompleteWorkAsync(agentId, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AgentProfile> CompleteWorkAsync(string agentId, AgentStateChangeContext context, CancellationToken cancellationToken = default)
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

        if (profile is null)
        {
            return null;
        }

        if (profile.PresenceStatus is not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp ||
            !string.IsNullOrWhiteSpace(profile.ActiveReservationId))
        {
            return null;
        }

        var previousStatus = profile.PresenceStatus;
        var targetStatus = profile.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(profile);

        // A pending state somebody asked for while the work was in flight is a request taking effect; a pending
        // state routing captured to return the agent to is just the work ending.
        var requestApplied = profile.RequestedPresenceStatus.HasValue && profile.PresenceRequestedUtc.HasValue;
        var source = context?.Source is null or AgentStateChangeSources.WorkCompleted
            ? requestApplied ? AgentStateChangeSources.RequestApplied : AgentStateChangeSources.WorkCompleted
            : context.Source;

        profile.RequestedPresenceStatus = null;
        profile.ActiveReservationId = null;

        var actor = context?.Actor ?? ContactCenterActor.System;

        var change = await _stateTransitions.TransitionAsync(profile, targetStatus, new AgentStateChangeContext
        {
            Actor = actor,
            Source = source,
            ReasonCodeId = requestApplied ? profile.PresenceReasonCodeId : null,
            ReasonName = requestApplied ? profile.PresenceReason : null,
            InteractionId = context?.InteractionId,
            ReservationId = context?.ReservationId,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(profile.UserId, cancellationToken),
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        // Round-robin fairness turns on who least recently finished work, so the stamp is taken here - at the end
        // of the work - rather than when an offer was pushed at the agent, which they may never have accepted.
        profile.LastWorkCompletedUtc = _clock.UtcNow;
        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }
}
