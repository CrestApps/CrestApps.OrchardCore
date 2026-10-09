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
    private readonly IEnumerable<IRecordingDisclosureService> _disclosureServices;
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
        IEnumerable<IRecordingDisclosureService> disclosureServices,
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
        _disclosureServices = disclosureServices;
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

    /// <inheritdoc />
    public async Task AnnounceAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
        var announcement = interaction is null ? null : EntryPointAnnouncement.Read(interaction);

        // Only a caller the routing has just marked as owed a message is announced to: one that is already hearing it,
        // or has had it, is never given it twice.
        if (announcement is null || announcement.Status != EntryPointAnnouncement.Scheduled)
        {
            return;
        }

        // A caller who hung up before the routing that created their call committed has nothing to hear.
        if (interaction.IsSettled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The entry point's {AnnouncementKind} message was not said on interaction '{InteractionId}': the call ended before it could start.",
                    announcement.Kind,
                    interactionId.SanitizeLogValue());
            }

            return;
        }

        var entryPoint = await _flowResolver.FindEntryPointAsync(interaction, cancellationToken);

        if (entryPoint is null)
        {
            _logger.LogWarning(
                "The entry point for interaction '{InteractionId}' no longer exists, so its {AnnouncementKind} message cannot be said and the caller cannot be put through.",
                interactionId.SanitizeLogValue(),
                announcement.Kind);

            return;
        }

        var message = announcement.Kind == EntryPointAnnouncement.Closed ? entryPoint.ClosedMessage : entryPoint.WelcomeMessage;
        var disclosureService = _disclosureServices.FirstOrDefault();
        var disclosure = announcement.IncludesDisclosure && disclosureService is not null
            ? await disclosureService.GetDisclosureAsync(RecordingDisclosureCallType.Inbound, cancellationToken)
            : null;

        // The disclosure was turned off after the call arrived: the caller hears the message alone, and is not recorded
        // as having been told. Saved with the message's status below.
        if (announcement.IncludesDisclosure && string.IsNullOrWhiteSpace(disclosure))
        {
            EntryPointAnnouncement.SetIncludesDisclosure(interaction, false);
        }

        // Said as one message so nothing can come between them: the disclosure first, because it is the first thing a
        // caller must hear, then the entry point's own message.
        var text = string.Join(' ', new[] { disclosure, message }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim()));

        // The message was removed after the call arrived: the caller goes on exactly as they would have without one.
        if (string.IsNullOrWhiteSpace(text))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The entry point's {AnnouncementKind} message was skipped on interaction '{InteractionId}' because entry point '{EntryPointId}' no longer has one; the caller goes on to {AnnouncementNext}.",
                    announcement.Kind,
                    interactionId.SanitizeLogValue(),
                    entryPoint.ItemId.SanitizeLogValue(),
                    announcement.Next);
            }

            EntryPointAnnouncement.SetStatus(interaction, EntryPointAnnouncement.Skipped);
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            await ContinueAfterAnnouncementAsync(interaction, entryPoint, announcement, cancellationToken);

            return;
        }

        // A closed entry point that turns callers away ends the call itself once the message has been said, so nothing
        // has to wait for the end of the speech to hang up.
        var endCallAfter = announcement.Next == EntryPointAnnouncement.NextReject;
        var accepted = await _ivrExecutionService.AnnounceAsync(interaction, text, endCallAfter, cancellationToken);

        // The message's status was committed before the provider was asked, and a commit forgets what the session had
        // loaded, so the interaction is read again rather than saved from the copy taken before.
        var current = await _interactionManager.FindByIdAsync(interactionId, cancellationToken) ?? interaction;

        if (!accepted)
        {
            _logger.LogWarning(
                "The entry point's {AnnouncementKind} message could not be said on interaction '{InteractionId}' (the provider cannot speak on this call, or refused); the caller goes on to {AnnouncementNext} without it.",
                announcement.Kind,
                interactionId.SanitizeLogValue(),
                announcement.Next);

            EntryPointAnnouncement.SetStatus(current, EntryPointAnnouncement.Failed);
            await _interactionManager.UpdateAsync(current, cancellationToken: cancellationToken);
            await ContinueAfterAnnouncementAsync(current, entryPoint, announcement, cancellationToken);

            return;
        }

        if (endCallAfter)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The entry point's {AnnouncementKind} message is being said on interaction '{InteractionId}'; the provider ends the call once it has been said.",
                    announcement.Kind,
                    interactionId.SanitizeLogValue());
            }

            EntryPointAnnouncement.SetStatus(current, EntryPointAnnouncement.Played);
            await _interactionManager.UpdateAsync(current, cancellationToken: cancellationToken);

            if (!await _inboundProcessor.EndInboundAsync(current.ActivityItemId, EntryPointAnnouncement.ClosedRejectReasonCode, providerEndsCall: true, cancellationToken))
            {
                _logger.LogWarning(
                    "The call on interaction '{InteractionId}' could not be recorded as ended after the entry point's closed message.",
                    interactionId.SanitizeLogValue());
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The entry point's {AnnouncementKind} message is being said on interaction '{InteractionId}'; the caller goes on to {AnnouncementNext} when the provider reports that it has ended.",
                announcement.Kind,
                interactionId.SanitizeLogValue(),
                announcement.Next);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CompleteAnnouncementAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
        var announcement = interaction is null ? null : EntryPointAnnouncement.Read(interaction);

        // The end of any other speech on the call (a queue announcement, a repeated report of this one) is not the end
        // of a message the caller is waiting on, and must not move them a second time.
        if (announcement is null || announcement.Status != EntryPointAnnouncement.Speaking)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Speech ended on interaction '{InteractionId}', which is not waiting on an entry point message (status '{AnnouncementStatus}'); nothing to do.",
                    interactionId.SanitizeLogValue(),
                    announcement?.Status);
            }

            return false;
        }

        if (interaction.IsSettled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The entry point's {AnnouncementKind} message ended on interaction '{InteractionId}' after the call had already ended; the caller is not moved on.",
                    announcement.Kind,
                    interactionId.SanitizeLogValue());
            }

            return false;
        }

        var entryPoint = await _flowResolver.FindEntryPointAsync(interaction, cancellationToken);

        if (entryPoint is null)
        {
            _logger.LogWarning(
                "The entry point for interaction '{InteractionId}' no longer exists, so the caller who heard its {AnnouncementKind} message cannot be put through.",
                interactionId.SanitizeLogValue(),
                announcement.Kind);

            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The entry point's {AnnouncementKind} message was said on interaction '{InteractionId}'; the caller goes on to {AnnouncementNext}.",
                announcement.Kind,
                interactionId.SanitizeLogValue(),
                announcement.Next);
        }

        // Left to commit with the routing it causes, like a menu choice: committed on its own, a routing that then
        // failed on a concurrency conflict would be retried against a caller already marked as moved on, and do nothing.
        EntryPointAnnouncement.SetStatus(interaction, EntryPointAnnouncement.Played);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        // Recorded before the caller is put through, so the consent it captures is there when the call connects to an
        // agent and the recording asks for it.
        var disclosureService = _disclosureServices.FirstOrDefault();

        if (announcement.IncludesDisclosure && disclosureService is not null)
        {
            await disclosureService.RecordDisclosedAsync(interaction.ItemId, ContactCenterConstants.RecordingDisclosureMethod.Announcement, cancellationToken);
        }

        await ContinueAfterAnnouncementAsync(interaction, entryPoint, announcement, cancellationToken);

        return true;
    }

    private async Task ContinueAfterAnnouncementAsync(
        Interaction interaction,
        ContactCenterEntryPoint entryPoint,
        EntryPointAnnouncement announcement,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            return;
        }

        switch (announcement.Next)
        {
            case EntryPointAnnouncement.NextMenu:
                // The menu starts here rather than through StartAsync, which answers a fresh caller: this one has been
                // answered and welcomed, and only the menu is left. Going back to the main menu later is a menu step,
                // not a new start, so the welcome is never said again.
                var step = await _ivrExecutionService.StartAsync(interaction, entryPoint.IvrFlow, cancellationToken);
                await RouteAsync(interaction.ItemId, entryPoint, step, cancellationToken);
                break;

            case EntryPointAnnouncement.NextQueue:
                await RouteToQueueAsync(interaction, entryPoint, announcement.QueueId, isEntryPointTarget: false, cancellationToken);
                break;

            case EntryPointAnnouncement.NextVoicemail:
                await SendToVoicemailAsync(interaction, entryPoint, EntryPointAnnouncement.ClosedVoicemailReasonCode, cancellationToken);
                break;

            case EntryPointAnnouncement.NextReject:
                // Reached only when the message could not be said: the call is turned away the way it always was.
                if (!await _inboundProcessor.EndInboundAsync(interaction.ActivityItemId, EntryPointAnnouncement.ClosedRejectReasonCode, providerEndsCall: false, cancellationToken))
                {
                    _logger.LogWarning(
                        "The call on interaction '{InteractionId}' could not be rejected after the entry point's closed message.",
                        interaction.ItemId.SanitizeLogValue());
                }

                break;

            default:
                await RouteToEntryPointTargetAsync(interaction, entryPoint, cancellationToken);
                break;
        }
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
        // It goes to the line's mailbox, when one is set: the entry point's voicemail inbox agent, or the shared box of
        // the queue. The call normally carries it from when it arrived; a call that does not is given it here.
        if (VoicemailDelivery.StampMailbox(interaction, entryPoint, interaction.QueueId))
        {
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
