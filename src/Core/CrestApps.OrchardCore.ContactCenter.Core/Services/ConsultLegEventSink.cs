using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed class ConsultLegEventSink : IConsultLegEventSink
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IConsultTransferService _consults;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IContactCenterAgentLegFailureService _agentLegService;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsultLegEventSink"/> class.
    /// </summary>
    public ConsultLegEventSink(
        ICallSessionManager callSessionManager,
        IInteractionManager interactionManager,
        IConsultTransferService consults,
        IAgentPresenceManager presenceManager,
        IContactCenterAgentLegFailureService agentLegService,
        ISession session,
        IClock clock)
    {
        _callSessionManager = callSessionManager;
        _interactionManager = interactionManager;
        _consults = consults;
        _presenceManager = presenceManager;
        _agentLegService = agentLegService;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<bool> OnAnsweredAsync(
        string providerName,
        string providerCallId,
        string consultId,
        string consultLegId,
        CancellationToken cancellationToken = default)
    {
        var (session, consult) = await FindAsync(providerName, providerCallId, consultId, cancellationToken);

        if (consult is null || !await _consults.MarkConnectedAsync(session.ItemId, consult.ConsultId, cancellationToken))
        {
            return false;
        }

        if (consult.TargetType == InteractionTransferTargetType.Agent && !string.IsNullOrEmpty(consult.TargetId))
        {
            // On a call now, so routing must not ring them with the next queued caller mid-consult.
            await _presenceManager.StartConsultWorkAsync(consult.TargetId, new AgentStateChangeContext
            {
                InteractionId = session.InteractionId,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<ConsultLegEndedOutcome> OnEndedAsync(
        string providerName,
        string providerCallId,
        string consultId,
        string consultLegId,
        HangupCause? hangupCause,
        DateTime? endedUtc,
        CancellationToken cancellationToken = default)
    {
        var (session, consult) = await FindAsync(providerName, providerCallId, consultId, cancellationToken);

        if (consult is null)
        {
            return ConsultLegEndedOutcome.Ignored;
        }

        if (consult.Status is ConsultCallStatus.Initiated or ConsultCallStatus.Ringing or ConsultCallStatus.Connected)
        {
            return await ReturnToAgentAsync(session, consult, cancellationToken);
        }

        if (consult.Status != ConsultCallStatus.Completed)
        {
            return ConsultLegEndedOutcome.Ignored;
        }

        // The consulted party took the call over and has now hung up on it, which ends it.
        if (consult.TargetType == InteractionTransferTargetType.Agent)
        {
            return await _agentLegService.RecordEndedAsync(providerName, providerCallId, consultLegId, endedUtc, hangupCause ?? HangupCause.NormalClearing, cancellationToken)
                ? ConsultLegEndedOutcome.CallEnded
                : ConsultLegEndedOutcome.Ignored;
        }

        // The call already left the contact center when it was handed to the external party; only the provider's
        // leg to the caller is left to release.
        return ConsultLegEndedOutcome.ReleaseCustomer;
    }

    private async Task<ConsultLegEndedOutcome> ReturnToAgentAsync(CallSession session, ConsultCall consult, CancellationToken cancellationToken)
    {
        var wasConnected = consult.Status == ConsultCallStatus.Connected;

        if (!await _consults.EndByTargetAsync(session.ItemId, consult.ConsultId, cancellationToken))
        {
            return ConsultLegEndedOutcome.Ignored;
        }

        var interaction = await _interactionManager.FindByIdAsync(session.InteractionId, cancellationToken);

        if (interaction is not null && InteractionTransferHistory.AbandonPending(interaction, InteractionTransferHistory.ConsultCancelled))
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        if (wasConnected && consult.TargetType == InteractionTransferTargetType.Agent && !string.IsNullOrEmpty(consult.TargetId))
        {
            await _presenceManager.CompleteWorkAsync(consult.TargetId, new AgentStateChangeContext
            {
                InteractionId = session.InteractionId,
                ChangedUtc = _clock.UtcNow,
            }, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return ConsultLegEndedOutcome.ReturnedToAgent;
    }

    private async Task<(CallSession Session, ConsultCall Consult)> FindAsync(
        string providerName,
        string providerCallId,
        string consultId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(consultId))
        {
            return (null, null);
        }

        var session = string.IsNullOrWhiteSpace(providerName)
            ? await _callSessionManager.FindByProviderCallIdAsync(providerCallId, cancellationToken)
            : await _callSessionManager.FindByProviderCallIdAsync(providerName, providerCallId, cancellationToken);

        var consult = session?.Consults.FirstOrDefault(candidate =>
            candidate is not null && string.Equals(candidate.ConsultId, consultId, StringComparison.Ordinal));

        return (session, consult);
    }
}
