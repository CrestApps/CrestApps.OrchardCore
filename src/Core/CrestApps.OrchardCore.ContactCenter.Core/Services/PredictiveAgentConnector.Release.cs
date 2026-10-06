using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Giving up on a claimed agent whose leg does not answer, and the sweep that settles answered calls nothing connected.
/// </summary>
public sealed partial class PredictiveAgentConnector
{
    /// <inheritdoc/>
    public async Task<bool> ReleaseUnansweredAgentLegAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetConnectLockKey(interactionId),
            TimeSpan.Zero,
            _options.PacingLockExpiration);

        if (!locked)
        {
            // Another delivery is working on the call right now; look again shortly rather than act beside it.
            ScheduleAgentLegDeadline(interactionId, _clock.UtcNow.Add(_connectRetryDelay));

            return false;
        }

        await using var acquiredLock = locker;

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        // The agent joined, the call ended, or it was dealt with some other way: nothing is left to give up on. The marks
        // are what make this safe to race the agent's answer and the sweep.
        if (interaction is null ||
            interaction.IsSettled ||
            !DialerCallMetadata.IsOverDialed(interaction) ||
            !DialerCallMetadata.IsAgentClaimed(interaction) ||
            DialerCallMetadata.HasAgentJoined(interaction) ||
            DialerCallMetadata.IsAbandoned(interaction))
        {
            return false;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is null || CallSessionLifecycle.IsTerminal(session.State) || string.IsNullOrEmpty(session.ProviderCallId))
        {
            return false;
        }

        // Read before the call is settled, which ends every leg it records: the agent leg still ringing is the one to hang up.
        var pendingLegId = session.Legs.LastOrDefault(leg =>
            leg.Role == CallPartyRole.Agent &&
            !leg.EndedUtc.HasValue &&
            !leg.AnsweredUtc.HasValue &&
            !string.IsNullOrEmpty(leg.ProviderLegId))?.ProviderLegId;
        var providerName = session.ProviderName;
        var agentId = interaction.AgentId;
        var claimedUtc = DialerCallMetadata.GetAgentClaimedUtc(interaction);

        // The same path a failed agent leg takes: the message, the call settled, the agent back to work with nothing to
        // wrap up. It is recorded as a timeout, not a failure.
        var released = await _agentLegFailureService.FailAsync(
            session.ProviderName,
            session.ProviderCallId,
            hangupCause: null,
            DialerAbandonment.Reasons.AgentLegTimeout,
            cancellationToken);

        // Committed before the agent's leg is hung up: that hang-up comes back as the leg failing, and must find the call
        // already settled rather than abandon it a second time.
        await _session.SaveChangesAsync(cancellationToken);

        // The agent's phone stops ringing, and a late answer can no longer join them to a person hearing the message.
        if (!string.IsNullOrEmpty(pendingLegId))
        {
            await HangUpLegAsync(providerName, pendingLegId, interaction);
        }

        _logger.LogWarning(
            "The leg of agent '{AgentId}' claimed for over-dialed call '{ProviderCallId}' (interaction '{InteractionId}') did not answer within {Milliseconds:0} ms of the claim; the agent was released and the call abandoned.",
            agentId.SanitizeLogValue(),
            session.ProviderCallId.SanitizeLogValue(),
            interaction.ItemId.SanitizeLogValue(),
            claimedUtc.HasValue ? (_clock.UtcNow - claimedUtc.Value).TotalMilliseconds : _options.AgentLegAnswerTimeout.TotalMilliseconds);

        var queueId = session.QueueId ?? interaction.QueueId;

        if (!string.IsNullOrEmpty(queueId))
        {
            _pacingScheduler.Request(queueId);
        }

        return released;
    }

    /// <inheritdoc/>
    public async Task<int> SweepAnsweredUnconnectedAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var settled = 0;

        // Answered, and never claimed or abandoned: the connect that should have run at the answer was lost.
        foreach (var queueId in await _queueItemStore.GetDialerInFlightQueueIdsAsync(cancellationToken))
        {
            foreach (var item in await _queueItemStore.GetDialerInFlightAsync(queueId, cancellationToken))
            {
                var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);
                var answeredUtc = interaction is null ? null : DialerCallMetadata.GetLiveAnsweredUtc(interaction);

                if (interaction is null ||
                    interaction.IsSettled ||
                    !DialerCallMetadata.IsOverDialed(interaction) ||
                    DialerCallMetadata.WasAnsweredByMachine(interaction) ||
                    answeredUtc is null ||
                    answeredUtc.Value.Add(_options.AnsweredUnconnectedSweepAfter) > now ||
                    DialerCallMetadata.IsAgentClaimed(interaction) ||
                    DialerCallMetadata.IsAbandoned(interaction))
                {
                    continue;
                }

                if (await InOwnScopeAsync(connector => connector.AbandonUnconnectedAsync(interaction.ItemId, queueId, cancellationToken)))
                {
                    settled++;
                }
            }
        }

        // Claimed, and the agent never joined: the deadline that gives up on the agent's leg was lost.
        foreach (var item in await _queueItemStore.GetDialerClaimedAsync(cancellationToken))
        {
            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);
            var claimedUtc = interaction is null ? null : DialerCallMetadata.GetAgentClaimedUtc(interaction);

            if (interaction is null ||
                interaction.IsSettled ||
                !DialerCallMetadata.IsOverDialed(interaction) ||
                claimedUtc is null ||
                claimedUtc.Value.Add(_options.AgentLegAnswerTimeout).Add(_options.AnsweredUnconnectedSweepAfter) > now ||
                DialerCallMetadata.HasAgentJoined(interaction) ||
                DialerCallMetadata.IsAbandoned(interaction))
            {
                continue;
            }

            if (await InOwnScopeAsync(connector => connector.ReleaseUnansweredAgentLegAsync(interaction.ItemId, cancellationToken)))
            {
                settled++;
            }
        }

        if (settled > 0)
        {
            _logger.LogWarning("The answered-call sweep settled {Count} over-dialed call(s) that nothing had connected or abandoned.", settled);
        }

        return settled;
    }

    internal async Task<bool> AbandonUnconnectedAsync(string interactionId, string queueId, CancellationToken cancellationToken)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetConnectLockKey(interactionId),
            TimeSpan.Zero,
            _options.PacingLockExpiration);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        // Read again under the lock: a connect may have claimed or abandoned it since the sweep looked.
        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null ||
            interaction.IsSettled ||
            DialerCallMetadata.IsAgentClaimed(interaction) ||
            DialerCallMetadata.IsAbandoned(interaction) ||
            !string.IsNullOrEmpty(interaction.AgentId))
        {
            return false;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        // A call whose session is gone or over still counts: the person answered and nobody came. The message is tried on
        // the call's own id, and the hang-up that follows a message that could not start is harmless on a call that ended.
        await AbandonAsync(
            interaction,
            session?.ProviderName ?? interaction.ProviderName,
            session?.ProviderCallId ?? interaction.ProviderInteractionId,
            queueId,
            DialerAbandonment.Reasons.AnsweredUnconnected,
            cancellationToken);

        return true;
    }

    // Each call the sweep settles commits in a scope of its own, so one that loses a race cannot spend the session of the
    // others, and a commit never detaches documents the loop read before it.
    private async Task<bool> InOwnScopeAsync(Func<PredictiveAgentConnector, Task<bool>> work)
    {
        var result = false;

        try
        {
            await _scopeExecutor.ExecuteAsync<IPredictiveAgentConnector>(async connector =>
            {
                if (connector is PredictiveAgentConnector predictive)
                {
                    result = await work(predictive);
                }
            });
        }
        catch (ConcurrencyException)
        {
            // Something else settled the call at the same moment; it is no longer the sweep's to settle.
            result = false;
        }

        return result;
    }

    // The provider's own ring limit is the backstop for the agent-leg deadline when the node holding it stops: whole seconds,
    // never under the five most providers accept, and past the deadline so the deadline acts first.
    private int AgentLegProviderTimeoutSeconds()
        => Math.Max(5, (int)Math.Ceiling(_options.AgentLegAnswerTimeout.TotalSeconds) + 2);

    private void ScheduleAgentLegDeadline(string interactionId, DateTime dueUtc)
        => _deadlineScheduler.Schedule(
            GetAgentLegDeadlineKey(interactionId),
            dueUtc,
            async (services, token) =>
            {
                await services.GetRequiredService<IPredictiveAgentConnector>().ReleaseUnansweredAgentLegAsync(interactionId, token);

                return null;
            });
}
