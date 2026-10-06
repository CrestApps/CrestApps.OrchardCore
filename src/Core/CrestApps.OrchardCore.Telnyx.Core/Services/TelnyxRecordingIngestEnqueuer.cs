using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Contact Center handler for a finished Telnyx recording: it correlates the recording to the interaction
/// that owns it (through the recording <c>client_state</c> the platform set when recording started), stamps the
/// interaction with the recording's retrieval handle, and enqueues a durable ingest job that downloads the
/// recording into the encrypted media store. Enqueueing is idempotent per recording, so a redelivered
/// saved-recording webhook never creates a duplicate job.
/// </summary>
public sealed class TelnyxRecordingIngestEnqueuer : ITelnyxRecordingSavedHandler
{
    private readonly ITelnyxRecordingIngestJobStore _jobStore;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IContactCenterEventPublisher _eventPublisher;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ICallRecordingCatalog _callRecordingCatalog;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxRecordingIngestEnqueuer> _logger;

    // The agent profile manager is owned by the Agents feature. Voicemail requires agents, but the recording
    // ingest itself does not, so resolve it optionally rather than taking a hard dependency on that feature.

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestEnqueuer"/> class.
    /// </summary>
    /// <param name="jobStore">The durable recording ingest job store.</param>
    /// <param name="interactionManager">The interaction manager used to stamp the recording retrieval handle.</param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve a voicemail's recipient agent.</param>
    /// <param name="eventPublisher">The Contact Center event publisher used to surface a saved voicemail to its recipient.</param>
    /// <param name="scopeExecutor">The scope executor used to ingest the recording right after the webhook commits.</param>
    /// <param name="callRecordingCatalogs">The call recordings catalog, when the Call Recording feature is enabled, which lists each recorded call.</param>
    /// <param name="clock">The clock used to stamp the job's creation and first-due time.</param>
    /// <param name="logger">The logger instance.</param>
    public TelnyxRecordingIngestEnqueuer(
        ITelnyxRecordingIngestJobStore jobStore,
        IInteractionManager interactionManager,
        IEnumerable<IAgentProfileManager> agentProfileManagers,
        IContactCenterEventPublisher eventPublisher,
        IContactCenterScopeExecutor scopeExecutor,
        IEnumerable<ICallRecordingCatalog> callRecordingCatalogs,
        IClock clock,
        ILogger<TelnyxRecordingIngestEnqueuer> logger)
    {
        _callRecordingCatalog = callRecordingCatalogs.FirstOrDefault();
        _jobStore = jobStore;
        _interactionManager = interactionManager;
        _agentProfileManager = agentProfileManagers.FirstOrDefault();
        _eventPublisher = eventPublisher;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (string.IsNullOrWhiteSpace(callEvent.RecordingId))
        {
            return false;
        }

        // A voicemail is recorded with a recording client_state naming its interaction: the caller's leg ends with the
        // message, so nothing else needs the leg's own state afterwards.
        if (TelnyxRecordingClientState.TryParse(callEvent.ClientState, out var recordingState))
        {
            var voicemailInteraction = await _interactionManager.FindByIdAsync(recordingState.InteractionId, cancellationToken);

            return voicemailInteraction is not null &&
                await EnqueueInteractionRecordingAsync(callEvent, voicemailInteraction, recordingState.IsVoicemail, recordingState.RecipientUserId, cancellationToken);
        }

        // A call recording carries no client_state, which would replace the state the leg's own events need. It is
        // traced through the leg Telnyx names instead: a recording the platform listed when it started it (an
        // automated voice agent's call, or a number dialed on the soft phone), else the interaction whose call it is.
        if (_callRecordingCatalog is not null &&
            await _callRecordingCatalog.FindRunningAsync(callEvent.CallControlId, cancellationToken) is { } running)
        {
            return await EnqueueCallRecordingAsync(callEvent, running, cancellationToken);
        }

        var interaction = string.IsNullOrWhiteSpace(callEvent.CallControlId)
            ? null
            : await _interactionManager.FindByProviderInteractionIdAsync(TelnyxConstants.ProviderTechnicalName, callEvent.CallControlId, cancellationToken);

        if (interaction is null)
        {
            // Not started by the platform (for example, a connection-level recording), so nothing owns it here.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Telnyx recording {RecordingId} on leg {CallControlId} matches no recording the platform started, so it was not stored.",
                    callEvent.RecordingId.SanitizeLogValue(),
                    callEvent.CallControlId.SanitizeLogValue());
            }

            return false;
        }

        return await EnqueueInteractionRecordingAsync(callEvent, interaction, isVoicemail: false, recipientUserId: null, cancellationToken);
    }

    private async Task<bool> EnqueueInteractionRecordingAsync(
        TelnyxCallEvent callEvent,
        Interaction interaction,
        bool isVoicemail,
        string recipientUserId,
        CancellationToken cancellationToken)
    {
        // Stamp the interaction with the recording's retrieval handle now that the Telnyx recording id is known.
        // The recording id is the deterministic media-store storage key, so recording it here makes the recording
        // discoverable through the interaction the moment the encrypted copy lands, even though ingestion itself
        // runs asynchronously.

        interaction.RecordingReference = callEvent.RecordingId;
        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.ProviderRecordingId] = callEvent.RecordingId;
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = callEvent.RecordingId;
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.Format] = TelnyxConstants.Recording.Format;

        // A voicemail recording surfaces in the recipient agent's voicemail inbox. Flag the interaction and publish
        // the projection event once. The routing engine already flags (and projects) a call it sends to voicemail
        // before answering, so only flag here when it has not been flagged yet -- which covers an agent-initiated
        // "send to voicemail" from the soft phone, whose path does not go through the routing engine.
        var publishVoicemailProjection = false;

        if (isVoicemail &&
            !IsAlreadyFlaggedVoicemail(interaction))
        {
            interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;

            var recipientAgentId = await ResolveRecipientAgentIdAsync(recipientUserId, interaction, cancellationToken);

            if (!string.IsNullOrWhiteSpace(recipientAgentId))
            {
                interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = recipientAgentId;
                publishVoicemailProjection = true;
            }
        }

        try
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            // A voicemail is listened to from the voicemail inbox; every other recording of the call is listed on the
            // call recordings page.
            if (!isVoicemail && _callRecordingCatalog is not null)
            {
                await _callRecordingCatalog.RegisterAsync(new CallRecordingRegistration
                {
                    Source = CallRecordingSource.ContactCenter,
                    ProviderName = TelnyxConstants.ProviderTechnicalName,
                    ProviderRecordingId = callEvent.RecordingId,
                    StorageReference = callEvent.RecordingId,
                    Format = TelnyxConstants.Recording.Format,
                    InteractionId = interaction.ItemId,
                    ProviderCallId = callEvent.CallControlId,
                    StartedUtc = callEvent.RecordingStartedUtc,
                    EndedUtc = callEvent.RecordingEndedUtc,
                }, cancellationToken);
            }

            if (publishVoicemailProjection)
            {
                await _eventPublisher.PublishAsync(BuildVoicemailProjectionEvent(interaction, callEvent.RecordingId), cancellationToken);
            }

            await _jobStore.EnqueueAsync(
                interaction.ItemId,
                callEvent.RecordingId,
                TelnyxConstants.Recording.Format,
                _clock.UtcNow,
                cancellationToken);

            // Download the recording into the encrypted media store immediately rather than waiting for the next
            // scheduled ingest sweep, so a voicemail is playable within seconds of being left instead of up to a
            // minute later. The periodic sweep remains the durable retry for any prompt attempt that fails.
            _scopeExecutor.ScheduleAfterCommit<ITelnyxRecordingIngestService>(
                ingestService => ingestService.ProcessDueAsync(CancellationToken.None));

            return true;
        }
        // A ConcurrencyException means another process committed a conflicting change to the same interaction
        // (for example the routing engine or the direct-hold timeout stamping the call it just sent to voicemail).
        // Let it propagate: the durable provider webhook inbox lets the delivery's claim expire and re-dispatches
        // it in a fresh scope, which re-reads the interaction and stamps the recording. Swallowing it here would
        // settle the delivery as handled and silently drop the voicemail so it never surfaces on the soft phone.
        catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyException)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue Telnyx recording {RecordingId} for ingestion.",
                callEvent.RecordingId.SanitizeLogValue());

            return false;
        }
    }

    private async Task<bool> EnqueueCallRecordingAsync(
        TelnyxCallEvent callEvent,
        CallRecording running,
        CancellationToken cancellationToken)
    {
        try
        {
            // An AI call the AI handed to a person became an interaction partway through. The recording kept running
            // across the hand-off, so it is tied to that interaction too: the agent who took it then finds it among
            // their own calls, and erasing the interaction's recording erases this one.
            var handedOff = running.Source == CallRecordingSource.AiAgent && !string.IsNullOrEmpty(running.ActivityItemId)
                ? await _interactionManager.FindByActivityIdAsync(running.ActivityItemId, cancellationToken)
                : null;

            await _callRecordingCatalog.RegisterAsync(new CallRecordingRegistration
            {
                Source = running.Source,
                InteractionId = handedOff?.ItemId,
                ProviderName = TelnyxConstants.ProviderTechnicalName,
                ProviderRecordingId = callEvent.RecordingId,
                ProviderCallId = callEvent.CallControlId,
                StorageReference = callEvent.RecordingId,
                Format = TelnyxConstants.Recording.Format,
                StartedUtc = callEvent.RecordingStartedUtc,
                EndedUtc = callEvent.RecordingEndedUtc,
            }, cancellationToken);

            await _jobStore.EnqueueAsync(
                interactionId: null,
                callEvent.RecordingId,
                TelnyxConstants.Recording.Format,
                _clock.UtcNow,
                cancellationToken);

            _scopeExecutor.ScheduleAfterCommit<ITelnyxRecordingIngestService>(
                ingestService => ingestService.ProcessDueAsync(CancellationToken.None));

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyException)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue Telnyx recording {RecordingId} of a {Source} call for ingestion.",
                callEvent.RecordingId.SanitizeLogValue(),
                running.Source);

            return false;
        }
    }

    private static bool IsAlreadyFlaggedVoicemail(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            (value is bool boolean ? boolean : bool.TryParse(value?.ToString(), out var parsed) && parsed);
    }

    private async Task<string> ResolveRecipientAgentIdAsync(
        string recipientUserId,
        Interaction interaction,
        CancellationToken cancellationToken)
    {
        // Prefer the recipient carried on the recording (set when the call was sent to voicemail), which survives
        // even after the interaction has released its agent association; fall back to the interaction's agent.
        if (_agentProfileManager is not null && !string.IsNullOrWhiteSpace(recipientUserId))
        {
            var agent = await _agentProfileManager.FindByUserIdAsync(recipientUserId, cancellationToken);

            if (agent is not null && !string.IsNullOrWhiteSpace(agent.ItemId))
            {
                return agent.ItemId;
            }
        }

        return interaction.AgentId;
    }

    private InteractionEvent BuildVoicemailProjectionEvent(Interaction interaction, string recordingId)
    {
        return new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            CorrelationId = interaction.CorrelationId,
            ActorId = ContactCenterConstants.SystemActor,
            SourceComponent = ContactCenterConstants.Components.Voice,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = $"voicemail-recording-{recordingId}",
        };
    }
}
