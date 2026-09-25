using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed class TransferAgentReleaseService : ITransferAgentReleaseService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferAgentReleaseService"/> class.
    /// </summary>
    public TransferAgentReleaseService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IAgentPresenceManager presenceManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ILogger<TransferAgentReleaseService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _presenceManager = presenceManager;
        _voiceProviderResolver = voiceProviderResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReleaseAsync(
        Interaction interaction,
        CallSession session,
        string agentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var legIds = new List<string>();

        if (session is not null)
        {
            foreach (var leg in session.Legs.Where(leg => IsAgentsLiveLeg(session, leg, agentId)).ToArray())
            {
                // Ended with a cause, so the leg release that follows a call's end does not hang it up a second time.
                CallTopologyProjector.EndLeg(session, leg.ProviderLegId, utcNow, HangupCause.NormalClearing);
                legIds.Add(leg.ProviderLegId);
            }

            if (string.Equals(session.AgentId, agentId, StringComparison.Ordinal))
            {
                session.AgentId = null;
            }

            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        if (string.Equals(interaction.AgentId, agentId, StringComparison.Ordinal))
        {
            interaction.AgentId = null;
        }

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        // The same rule a call ending applies: after-call work is for queue and campaign work, and a direct call
        // leaves nothing to disposition. The queue is the one the call came in on, read before anything re-routes it.
        var context = new AgentStateChangeContext { InteractionId = interaction.ItemId, ChangedUtc = utcNow };

        if (ContactCenterConstants.QueueStartsAfterCallWork(interaction.QueueId))
        {
            await _presenceManager.StartWrapUpAsync(agentId, context, cancellationToken);
        }
        else
        {
            await _presenceManager.CompleteWorkAsync(agentId, context, cancellationToken);
        }

        return legIds;
    }

    /// <inheritdoc />
    public async Task<bool> DetachCallerAsync(
        Interaction interaction,
        CallSession session,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        // Only a leg the agent has of their own can take the caller with it when it is hung up.
        if (session is null ||
            !session.Legs.Any(leg => IsAgentsLiveLeg(session, leg, agentId)) ||
            _voiceProviderResolver.Get(interaction.ProviderName) is not IContactCenterVoiceCallerParkProvider provider)
        {
            return true;
        }

        var callerId = !string.IsNullOrWhiteSpace(session.ProviderCallId)
            ? session.ProviderCallId
            : interaction.ProviderInteractionId;

        if (string.IsNullOrWhiteSpace(callerId))
        {
            return false;
        }

        try
        {
            if (await provider.ParkCallerAsync(callerId, cancellationToken))
            {
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not take caller '{ProviderCallId}' out of the transferring agent's bridge.", callerId.SanitizeLogValue());
        }

        _logger.LogWarning(
            "The caller on '{ProviderCallId}' is still joined to the transferring agent's leg, so the transfer is not made: hanging that leg up would drop them.",
            callerId.SanitizeLogValue());

        return false;
    }

    /// <inheritdoc />
    public async Task HangUpAsync(string providerName, IEnumerable<string> agentLegIds, CancellationToken cancellationToken = default)
    {
        if (agentLegIds is null ||
            _voiceProviderResolver.Get(providerName) is not IContactCenterVoiceAgentLegReleaseProvider provider)
        {
            return;
        }

        foreach (var legId in agentLegIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            try
            {
                await provider.ReleaseAgentLegAsync(legId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The transfer has committed. A leg that does not hang up keeps the agent's phone on a silent line,
                // which they can end themselves; it must not undo the transfer.
                _logger.LogWarning(ex, "Could not hang up the transferring agent's leg '{AgentLegId}'.", legId.SanitizeLogValue());
            }
        }
    }

    private static bool IsAgentsLiveLeg(CallSession session, CallLeg leg, string agentId)
        => leg is not null &&
            leg.Role == CallPartyRole.Agent &&
            !leg.EndedUtc.HasValue &&
            !string.IsNullOrWhiteSpace(leg.ProviderLegId) &&
            !string.Equals(leg.ProviderLegId, session.ProviderCallId, StringComparison.Ordinal) &&
            (string.IsNullOrEmpty(leg.AgentId) || string.Equals(leg.AgentId, agentId, StringComparison.Ordinal));
}
