using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The other half of the queue's callback offer. The offer was spoken and a key collected, and then nothing was ever
/// done with the key: a caller who pressed 1 to be called back heard their music stop and stayed on hold.
/// </summary>
/// <remarks>
/// <para>
/// Accepting keeps the caller's place in line — the callback carries when they arrived, as the queued callback service
/// promises — takes them out of the queue so no agent is offered a caller who has gone, finishes the inbound activity,
/// tells them it is arranged, and ends the call once that has been said. The number called back is the one they are
/// calling from.
/// </para>
/// <para>
/// Any other answer — another key, no key, a key the provider could not read — is "no": the offer is made once, and
/// the caller is put straight back to their hold music and keeps waiting with the queue's treatment. A callback that
/// cannot be arranged (callbacks not enabled, or no number to call) is treated the same way rather than promised.
/// </para>
/// </remarks>
public sealed class QueueCallbackOfferResponder : IQueueCallbackOfferResponder
{
    /// <summary>
    /// The reason recorded on a caller who took a callback instead of waiting.
    /// </summary>
    public const string ReasonCode = "queued_callback";

    /// <summary>
    /// The interaction metadata key the callback's <see cref="ReasonCode"/> is written under, which the reports read
    /// when the callback's event is missing.
    /// </summary>
    internal const string RoutingTerminalReasonMetadataKey = "routing_terminal_reason";

    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueuedCallbackService _queuedCallbacks;
    private readonly IQueueTreatmentService _treatmentService;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IQueueTreatmentProvider _treatmentProvider;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueCallbackOfferResponder"/> class.
    /// </summary>
    public QueueCallbackOfferResponder(
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        IQueuedCallbackService queuedCallbacks,
        IQueueTreatmentService treatmentService,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IContactCenterScopeExecutor scopeExecutor,
        IQueueTreatmentProvider treatmentProvider,
        IContactCenterAuditRecorder auditRecorder,
        IClock clock,
        IStringLocalizer<QueueCallbackOfferResponder> stringLocalizer,
        ILogger<QueueCallbackOfferResponder> logger)
    {
        _auditRecorder = auditRecorder;
        S = stringLocalizer;
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _queuedCallbacks = queuedCallbacks;
        _treatmentService = treatmentService;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _activityWriter = activityWriter;
        _scopeExecutor = scopeExecutor;
        _treatmentProvider = treatmentProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(Interaction interaction, InboundVoiceDigitsEvent digitsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digitsEvent);

        if (interaction is null || interaction.IsSettled || string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            return false;
        }

        var item = await _queueItemManager.FindByActivityIdAsync(interaction.ActivityItemId, cancellationToken);

        // Only a caller who was played the offer is answering it. Anything else collecting a key on this call is
        // somebody else's to handle.
        if (item?.CallbackOfferedUtc is null)
        {
            return false;
        }

        // The press that accepted, delivered again: the callback is already arranged and the call is ending.
        if (item.CallbackAcceptedUtc is not null)
        {
            return true;
        }

        // An agent has already answered, or the caller has left the queue some other way; the offer is moot.
        if (item.Status is not (QueueItemStatus.Waiting or QueueItemStatus.Reserved))
        {
            return false;
        }

        // The collection was replaced by something else on the leg (an agent joined, a new prompt), or the caller
        // hung up: neither is an answer, and whatever replaced it owns what the caller hears.
        if (digitsEvent.Outcome is InboundVoiceDigitsOutcome.Cancelled or InboundVoiceDigitsOutcome.CallerHungUp)
        {
            return true;
        }

        var queue = await _queueManager.FindByIdAsync(item.QueueId, cancellationToken);

        // Offered to an agent while the offer was playing, the caller is about to be put through, so a press to be
        // called back is not acted on: they keep hearing the queue until the agent is joined.
        if (item.Status == QueueItemStatus.Waiting &&
            IsAcceptance(digitsEvent, queue) &&
            await AcceptAsync(interaction, item, cancellationToken))
        {
            return true;
        }

        await ResumeAsync(queue, interaction.ProviderInteractionId, cancellationToken);

        return true;
    }

    private static bool IsAcceptance(InboundVoiceDigitsEvent digitsEvent, ActivityQueue queue)
    {
        var acceptKey = queue?.Treatment?.CallbackDtmfKey?.Trim();

        return digitsEvent.Outcome == InboundVoiceDigitsOutcome.Collected &&
            !string.IsNullOrEmpty(acceptKey) &&
            string.Equals(digitsEvent.Digits?.Trim(), acceptKey, StringComparison.Ordinal);
    }

    private async Task<bool> AcceptAsync(Interaction interaction, QueueItem item, CancellationToken cancellationToken)
    {
        if (!await _queuedCallbacks.AcceptAsync(item, interaction.CustomerAddress, cancellationToken))
        {
            _logger.LogWarning(
                "The caller on interaction '{InteractionId}' pressed to be called back, but no callback could be arranged; they keep waiting in queue '{QueueId}'.",
                interaction.ItemId.SanitizeLogValue(),
                item.QueueId.SanitizeLogValue());

            return false;
        }

        var now = _clock.UtcNow;

        // The work continues as the callback; this inbound call is finished and nothing routes it any more.
        interaction.TechnicalMetadata[RoutingTerminalReasonMetadataKey] = ReasonCode;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        // Nobody answered this call and the caller did not give up on it: the reports count it as its own outcome,
        // with the wait measured to the moment they chose to be called back.
        await RecordCallbackRequestedAsync(interaction, item, now, cancellationToken);

        await _workStateService.MutateAsync(
            interaction.ActivityItemId,
            workState => workState.TransitionTo(ActivityAssignmentStatus.Released),
            cancellationToken);

        await _activityWriter.ScheduleUpdateAsync(interaction.ActivityItemId, activity =>
        {
            activity.Status = ActivityStatus.Completed;
            activity.TerminalReasonCode = ReasonCode;
            activity.CompletedUtc = now;
        }, cancellationToken);

        // Said once the callback is committed, so a caller is never told it is arranged when it was not. The call is
        // ended after the message rather than left for the caller to hang up on a silent line.
        var providerCallId = interaction.ProviderInteractionId;

        // Worded in the language the provider speaks prompts in, which is not the language of whoever's request this is.
        var confirmation = SpokenPromptCulture.Localize(_treatmentProvider.SpeechLanguage, () =>
            S["Thank you. We will call you back at the number you are calling from, and you will keep your place in line. Goodbye."].Value);

        if (!_scopeExecutor.ScheduleAfterCommit<IQueueTreatmentProvider>(provider =>
            provider.EndWithMessageAsync(providerCallId, confirmation, CancellationToken.None)))
        {
            await _treatmentProvider.EndWithMessageAsync(providerCallId, confirmation, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller on interaction '{InteractionId}' took a callback from queue '{QueueId}' instead of waiting.",
                interaction.ItemId.SanitizeLogValue(),
                item.QueueId.SanitizeLogValue());
        }

        return true;
    }

    private Task RecordCallbackRequestedAsync(Interaction interaction, QueueItem item, DateTime now, CancellationToken cancellationToken)
    {
        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.QueueId = item.QueueId;
        data.Reason = ReasonCode;
        data.DurationSeconds = ContactCenterCallAudit.QueueWaitSeconds(item, item.DequeuedUtc ?? now);
        data.Details["queueItemId"] = item.ItemId;

        return _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.CallbackRequested,
            data,
            now,
            new ContactCenterActor(ContactCenterActorType.Customer),
            $"callback-requested:{interaction.ItemId}",
            cancellationToken);
    }

    private async Task ResumeAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken)
    {
        try
        {
            await _treatmentService.StartWaitingAudioAsync(queue, providerCallId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not global::YesSql.ConcurrencyException)
        {
            // They are still in the queue either way; losing the music must not lose their place.
            _logger.LogWarning(ex, "Could not start the hold music again for a caller who kept waiting on call '{CallId}'.", providerCallId.SanitizeLogValue());
        }
    }
}
