using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
/// <remarks>
/// A menu choice is routed the way the entry point itself routes a call: a queue choice is admitted to the queue
/// (so a full queue still overflows or goes to voicemail), an agent choice rings that one agent the way a personal
/// line does (held for them, then voicemail after the entry point's ring window), and voicemail is the entry point's
/// own voicemail. The caller has already been answered to hear the menu, so a queued caller is given the queue's hold
/// treatment straight away rather than the silence an unanswered ringing call would have heard as ringback.
/// <para>
/// A choice that cannot be reached is never a dead end: the caller goes to the entry point's own target, and when that
/// cannot be reached either, to voicemail.
/// </para>
/// </remarks>
public sealed class IvrCallRouter : IIvrCallRouter
{
    /// <summary>
    /// The reason recorded when a caller chose voicemail from the menu.
    /// </summary>
    public const string VoicemailReasonCode = "ivr_voicemail";

    /// <summary>
    /// The reason recorded when nothing the menu could route to was reachable, so the caller was sent to voicemail.
    /// </summary>
    public const string UnroutableVoicemailReasonCode = "ivr_unroutable_voicemail";

    private readonly IInteractionManager _interactionManager;
    private readonly IEntryPointFlowResolver _flowResolver;
    private readonly IIvrExecutionService _ivrExecutionService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueLimitService _queueLimitService;
    private readonly IActivityQueueService _queueService;
    private readonly IVoiceQueueOfferService _offerService;
    private readonly IQueueTreatmentService _treatmentService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInboundVoiceCallProcessor _inboundProcessor;
    private readonly IIvrExternalTransferService _externalTransfers;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrCallRouter"/> class.
    /// </summary>
    public IvrCallRouter(
        IInteractionManager interactionManager,
        IEntryPointFlowResolver flowResolver,
        IIvrExecutionService ivrExecutionService,
        IActivityQueueManager queueManager,
        IQueueLimitService queueLimitService,
        IActivityQueueService queueService,
        IVoiceQueueOfferService offerService,
        IQueueTreatmentService treatmentService,
        IAgentProfileManager agentManager,
        IInboundVoiceCallProcessor inboundProcessor,
        IIvrExternalTransferService externalTransfers,
        IContactCenterAuditRecorder auditRecorder,
        ISession session,
        IClock clock,
        ILogger<IvrCallRouter> logger)
    {
        _interactionManager = interactionManager;
        _flowResolver = flowResolver;
        _ivrExecutionService = ivrExecutionService;
        _queueManager = queueManager;
        _queueLimitService = queueLimitService;
        _queueService = queueService;
        _offerService = offerService;
        _treatmentService = treatmentService;
        _agentManager = agentManager;
        _inboundProcessor = inboundProcessor;
        _externalTransfers = externalTransfers;
        _auditRecorder = auditRecorder;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        // A caller who hung up before the routing that created their call committed has nothing to hear.
        if (interaction is null || interaction.IsSettled)
        {
            return;
        }

        var entryPoint = await _flowResolver.FindEntryPointAsync(interaction, cancellationToken);

        if (entryPoint is null)
        {
            _logger.LogWarning(
                "The entry point for interaction '{InteractionId}' no longer exists, so its phone menu cannot be played.",
                interactionId.SanitizeLogValue());

            return;
        }

        var step = await _ivrExecutionService.StartAsync(interaction, entryPoint.IvrFlow, cancellationToken);

        await RouteAsync(interactionId, entryPoint, step, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RouteAsync(string interactionId, ContactCenterEntryPoint entryPoint, IvrStep step, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);
        ArgumentNullException.ThrowIfNull(entryPoint);

        if (step.Kind is IvrStepKind.Prompt or IvrStepKind.Ignored)
        {
            return;
        }

        // The menu committed the caller's position before it asked the provider for anything, and a commit forgets
        // what the session had loaded, so the interaction is read again rather than saved from a stale copy.
        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.IsSettled || string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            return;
        }

        switch (step.Kind)
        {
            case IvrStepKind.RouteToQueue:
                await RouteToQueueAsync(interaction, entryPoint, step.TargetId, isEntryPointTarget: false, cancellationToken);
                break;

            case IvrStepKind.RouteToAgent:
                await RouteToAgentAsync(interaction, entryPoint, step.TargetId, isEntryPointTarget: false, cancellationToken);
                break;

            case IvrStepKind.Voicemail:
                await SendToVoicemailAsync(interaction, entryPoint, VoicemailReasonCode, cancellationToken);
                break;

            case IvrStepKind.ExternalTransfer:
                if (!await _externalTransfers.TransferAsync(interaction, step.TargetId, cancellationToken))
                {
                    await RerouteAsync(interaction, entryPoint, "ExternalTransferUnavailable", step.TargetId, cancellationToken);
                }

                break;

            case IvrStepKind.Done:
                await RouteToEntryPointTargetAsync(interaction, entryPoint, cancellationToken);
                break;
        }
    }

    private async Task RouteToEntryPointTargetAsync(Interaction interaction, ContactCenterEntryPoint entryPoint, CancellationToken cancellationToken)
    {
        if (entryPoint.TargetType == EntryPointTargetType.Agent && !string.IsNullOrEmpty(entryPoint.TargetAgentId))
        {
            await RouteToAgentAsync(interaction, entryPoint, entryPoint.TargetAgentId, isEntryPointTarget: true, cancellationToken);

            return;
        }

        await RouteToQueueAsync(interaction, entryPoint, entryPoint.TargetQueueId, isEntryPointTarget: true, cancellationToken);
    }

    private async Task RouteToQueueAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        string queueId,
        bool isEntryPointTarget,
        CancellationToken cancellationToken)
    {
        var queue = string.IsNullOrEmpty(queueId) ? null : await _queueManager.FindByIdAsync(queueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            await UnreachableAsync(interaction, entryPoint, "QueueUnavailable", queueId, isEntryPointTarget, cancellationToken);

            return;
        }

        // A full queue does not take another caller because they came through a menu: its size limit decides
        // whether they wait in an overflow queue instead, or go to voicemail.
        var admission = await _queueLimitService.AdmitAsync(queue, cancellationToken);

        if (admission.Outcome == QueueAdmissionOutcome.Voicemail || !admission.IsQueued)
        {
            await SendToVoicemailAsync(interaction, entryPoint, ContactCenterConstants.QueueLimits.QueueFullVoicemailReasonCode, cancellationToken);

            return;
        }

        var effectiveQueueId = string.IsNullOrEmpty(admission.QueueId) ? queue.ItemId : admission.QueueId;
        var effectiveQueue = string.Equals(effectiveQueueId, queue.ItemId, StringComparison.Ordinal)
            ? queue
            : await _queueManager.FindByIdAsync(effectiveQueueId, cancellationToken) ?? queue;

        interaction.QueueId = effectiveQueueId;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _queueService.EnqueueAsync(interaction.ActivityItemId, effectiveQueueId, entryPoint.Priority, cancellationToken);

        var offeredUserId = await _offerService.OfferNextAsync(effectiveQueueId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A caller was put through to queue '{QueueId}' from the entry-point menu on interaction '{InteractionId}'; offered: {Offered}.",
                effectiveQueueId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                !string.IsNullOrEmpty(offeredUserId));
        }

        // The caller was answered to hear the menu, so the network is no longer ringing them: from here on they hear
        // whatever the queue plays. An offered caller is no longer a waiting one and would get nothing from the
        // treatment pass, so they get the music, or a ringing tone on a queue without any; a waiting one gets the
        // queue's treatment now rather than at the next sweep.
        if (string.IsNullOrEmpty(offeredUserId))
        {
            await RunTreatmentAsync(effectiveQueue, interaction.ProviderInteractionId, cancellationToken);
        }
        else
        {
            await StartWaitingAudioAsync(effectiveQueue, interaction.ProviderInteractionId, cancellationToken);
        }
    }

    private async Task RouteToAgentAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        string agentId,
        bool isEntryPointTarget,
        CancellationToken cancellationToken)
    {
        var agent = string.IsNullOrEmpty(agentId) ? null : await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            await UnreachableAsync(interaction, entryPoint, "AgentUnavailable", agentId, isEntryPointTarget, cancellationToken);

            return;
        }

        // Carried like a personal line: under the synthetic direct-routing queue, tagged for the one agent, with the
        // entry point's ring window. That is what lets the call be held and re-offered to that agent when they become
        // available, and sent to their voicemail when the window runs out.
        var ringTimeoutSeconds = EntryPointRoutingPlanner.ResolveDirectRingTimeout(entryPoint);

        interaction.QueueId = ContactCenterConstants.DirectRouting.QueueId;
        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = agent.ItemId;
        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey] = ringTimeoutSeconds;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _queueService.EnqueueAsync(interaction.ActivityItemId, ContactCenterConstants.DirectRouting.QueueId, entryPoint.Priority, cancellationToken);

        var offeredUserId = await _offerService.OfferToAgentAsync(
            interaction.ActivityItemId,
            ContactCenterConstants.DirectRouting.QueueId,
            agent.ItemId,
            ringTimeoutSeconds,
            cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A caller was put through to agent '{AgentId}' from the entry-point menu on interaction '{InteractionId}'; {Outcome}.",
                agent.ItemId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                string.IsNullOrEmpty(offeredUserId) ? "held for the agent" : "ringing");
        }

        // The menu answered the caller, so nothing is ringing on their side any more: while the agent's phone rings,
        // or while they are held for the agent, they hear the entry point's queue music when it routes to a queue,
        // and a ringing tone otherwise. It stops when the agent is joined or the caller goes to voicemail.
        var holdQueue = entryPoint.TargetType == EntryPointTargetType.Queue && !string.IsNullOrEmpty(entryPoint.TargetQueueId)
            ? await _queueManager.FindByIdAsync(entryPoint.TargetQueueId, cancellationToken)
            : null;

        await StartWaitingAudioAsync(holdQueue, interaction.ProviderInteractionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecoverFailedTransferAsync(string interactionId, string failedDestinationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.IsSettled)
        {
            return;
        }

        var entryPoint = await _flowResolver.FindEntryPointAsync(interaction, cancellationToken);

        if (entryPoint is null)
        {
            _logger.LogWarning(
                "The entry point for interaction '{InteractionId}' no longer exists, so the caller whose transfer failed cannot be put through anywhere.",
                interactionId.SanitizeLogValue());

            return;
        }

        await RouteAsync(interactionId, entryPoint, FallbackAfterFailedTransfer(entryPoint.IvrFlow, failedDestinationId), cancellationToken);
    }

    // What the menu does with a caller who has nowhere else to go is also what it does with one whose chosen number did
    // not answer. A fallback that is a menu is not replayed — the caller has already made their choice — and one that
    // is the number that just failed would ring it again, so both go to the entry point's own target instead.
    private static IvrStep FallbackAfterFailedTransfer(IvrFlow flow, string failedDestinationId)
    {
        var fallback = flow?.FallbackAction;

        var kind = fallback?.Kind switch
        {
            IvrActionKind.RouteToQueue => IvrStepKind.RouteToQueue,
            IvrActionKind.RouteToAgent => IvrStepKind.RouteToAgent,
            IvrActionKind.Voicemail => IvrStepKind.Voicemail,
            IvrActionKind.ExternalTransfer when !string.Equals(fallback.TargetId, failedDestinationId, StringComparison.OrdinalIgnoreCase) => IvrStepKind.ExternalTransfer,
            _ => IvrStepKind.Done,
        };

        return kind == IvrStepKind.Done
            ? IvrStep.Done with { IsFallback = true }
            : new IvrStep(kind, null, null, null, fallback.TargetId) { IsFallback = true };
    }

    private async Task SendToVoicemailAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        // A personal line's voicemail belongs to its agent, so a menu on one leaves the message in that agent's box.
        if (entryPoint.TargetType == EntryPointTargetType.Agent &&
            !string.IsNullOrEmpty(entryPoint.TargetAgentId) &&
            !interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.DirectRouting.TargetAgentMetadataKey))
        {
            interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = entryPoint.TargetAgentId;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        // A queue line has no agent of its own, so a message left on it went to nobody's inbox and was never heard.
        // It goes to the entry point's voicemail inbox, when one is set. The call normally carries it from when it
        // arrived; a call that does not is given it here.
        if (entryPoint.TargetType != EntryPointTargetType.Agent &&
            !string.IsNullOrEmpty(entryPoint.VoicemailRecipientAgentId) &&
            !interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.MailboxAgentMetadataKey))
        {
            interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.MailboxAgentMetadataKey] = entryPoint.VoicemailRecipientAgentId;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        if (!await _inboundProcessor.SendToVoicemailAsync(interaction.ActivityItemId, reasonCode, cancellationToken))
        {
            _logger.LogWarning(
                "The caller on interaction '{InteractionId}' could not be sent to voicemail from the entry-point menu.",
                interaction.ItemId.SanitizeLogValue());
        }
    }

    // What the menu chose could not be reached. The entry point's own target is tried next, and when that is what
    // could not be reached, voicemail: a caller who has made a choice is never simply left on the line.
    private async Task UnreachableAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        string reason,
        string targetId,
        bool isEntryPointTarget,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "The entry-point menu on interaction '{InteractionId}' chose '{TargetId}', which cannot be reached ({Reason}).",
            interaction.ItemId.SanitizeLogValue(),
            targetId.SanitizeLogValue(),
            reason);

        if (isEntryPointTarget)
        {
            await RecordRerouteAsync(interaction, reason, targetId, "Voicemail", cancellationToken);
            await SendToVoicemailAsync(interaction, entryPoint, UnroutableVoicemailReasonCode, cancellationToken);

            return;
        }

        await RerouteAsync(interaction, entryPoint, reason, targetId, cancellationToken);
    }

    private async Task RerouteAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        string reason,
        string targetId,
        CancellationToken cancellationToken)
    {
        await RecordRerouteAsync(interaction, reason, targetId, "EntryPointTarget", cancellationToken);
        await RouteToEntryPointTargetAsync(interaction, entryPoint, cancellationToken);
    }

    private Task RecordRerouteAsync(Interaction interaction, string reason, string targetId, string action, CancellationToken cancellationToken)
        => _auditRecorder.RecordIvrAsync(
            ContactCenterConstants.Events.IvrFallbackTaken,
            interaction,
            new IvrAuditStep
            {
                NodeId = IvrExecutionService.ReadState(interaction).CurrentNodeId,
                Action = action,
                Target = targetId,
                Reason = reason,
            },
            _clock.UtcNow,
            $"ivr:reroute:{interaction.ItemId}:{reason}:{targetId}:{action}",
            cancellationToken);

    private async Task RunTreatmentAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken)
    {
        try
        {
            // The treatment pass finds who is waiting by querying the store, so the new queue item is committed first.
            await _session.SaveChangesAsync(cancellationToken);
            await _treatmentService.RunDueAsync(queue, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyException)
        {
            // The caller is in the queue either way; losing the music must not lose the call.
            _logger.LogWarning(ex, "Could not start queue treatment for a caller routed from an entry-point menu to queue '{QueueId}'.", queue.ItemId.SanitizeLogValue());
        }

        // A queue that plays nothing at all was left to the network's ringing tone, which ended when the menu answered.
        if (queue.Treatment is null || !QueueTreatmentPolicy.PlaysAnything(queue.Treatment))
        {
            await StartWaitingAudioAsync(queue, providerCallId, cancellationToken);
        }
    }

    private async Task StartWaitingAudioAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        try
        {
            await _treatmentService.StartWaitingAudioAsync(queue, providerCallId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyException)
        {
            // The caller is on their way to a person either way; losing the audio must not lose the call.
            _logger.LogWarning(ex, "Could not start the waiting audio for a caller routed from an entry-point menu on call '{CallId}'.", providerCallId.SanitizeLogValue());
        }
    }
}
